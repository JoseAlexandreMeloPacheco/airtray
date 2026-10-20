using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;

internal sealed partial class ElginCloud
{
    internal JsonElement? TargetDevice { get; private set; }
    string? targetFamily;
    internal int FamiliesFound { get; private set; }
    internal int DevicesFound { get; private set; }
    internal sealed record DeviceChoice(string Name, string Family, JsonElement Endpoint);
    internal IReadOnlyList<DeviceChoice> AvailableDevices { get; private set; } = Array.Empty<DeviceChoice>();
    internal string? SelectedDeviceName { get; private set; }
    internal static string? Text(JsonElement element, string property)
    {
        if(element.ValueKind != JsonValueKind.Object) return null;
        foreach(var field in element.EnumerateObject()) if(field.Name.Equals(property, StringComparison.OrdinalIgnoreCase)) return field.Value.ValueKind == JsonValueKind.String ? field.Value.GetString() : field.Value.ValueKind == JsonValueKind.Number ? field.Value.GetRawText() : null;
        return null;
    }
    static JsonElement Get(JsonElement element, string property)
    {
        if(element.ValueKind == JsonValueKind.Object) foreach(var field in element.EnumerateObject()) if(field.Name.Equals(property, StringComparison.OrdinalIgnoreCase)) return field.Value;
        return default;
    }
    async Task<JsonDocument> Api(string path, object? payload, string? family = null)
    {
        if(!IsAuthenticated) throw new CloudException("Entre na conta antes de consultar o aparelho.");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("https://" + Hosts[currentRegion] + path));
        var json = payload == null ? "" : JsonSerializer.Serialize(payload);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        if(path.StartsWith("/device/control/",StringComparison.Ordinal)) request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-java-serialized-object");
        request.Headers.Add("userid", UserId);
        request.Headers.Add("loginsession", Session);
        request.Headers.Add("licenseid", LicenseId);
        request.Headers.Add("lid", LicenseId);
        request.Headers.Add("loginmode", "mutuallyexclusive");
        request.Headers.Add("language", "pt-BR");
        request.Headers.Add("system", "android");
        request.Headers.Add("appPlatform", "android");
        request.Headers.Add("appVersion", "1.0.1_2238f981e");
        request.Headers.Add("timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        request.Headers.Add("messageId", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
        if(family != null) request.Headers.Add("familyid", family);
        try
        {
            using var deadline=new CancellationTokenSource(http.Timeout);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            if(response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                SignOut(); throw new CloudException("O serviço recusou esta sessão. Entre novamente na sua conta.");
            }
            if(!response.IsSuccessStatusCode) throw new CloudException("O serviço de aparelhos retornou HTTP " + (int)response.StatusCode + ".");
            await response.Content.LoadIntoBufferAsync(1048576,deadline.Token);
            var content = await response.Content.ReadAsStringAsync(deadline.Token);
            JsonDocument document;
            try { document = JsonDocument.Parse(content); }
            catch(JsonException) { throw new CloudException("A consulta retornou uma resposta inesperada."); }
            var root = document.RootElement;
            var code = Text(root, "status") ?? Text(root, "error");
            if(code != "0" && !(code == null && path.StartsWith("/device/control/", StringComparison.Ordinal))) { document.Dispose(); throw new CloudException("A consulta do aparelho falhou (código " + (code ?? "ausente") + ")."); }
            return document;
        }
        catch(HttpRequestException) { throw new CloudException("Falha ao acessar o serviço de aparelhos."); }
        catch(OperationCanceledException) { throw new CloudException("O aparelho ou o servidor não respondeu a tempo."); }
    }

    internal async Task<bool> FindTarget()
    {
        await DiscoverDevices();
        if(AvailableDevices.Count > 1) throw new CloudException("Há mais de um ar compatível. Escolha o aparelho no painel.");
        if(AvailableDevices.Count == 0) return false;
        SelectDevice(AvailableDevices[0]); return true;
    }

    internal async Task DiscoverDevices()
    {
        TargetDevice = null; targetFamily = null; FamiliesFound = 0; DevicesFound = 0;
        AvailableDevices = Array.Empty<DeviceChoice>(); SelectedDeviceName = null;
        using var families = await Api("/appsync/group/member/getfamilylist", null);
        var list = Get(Get(families.RootElement, "data"), "familyList");
        if(list.ValueKind != JsonValueKind.Array) throw new CloudException("O serviço não retornou a lista de casas esperada.");
        FamiliesFound = list.GetArrayLength();
        if(FamiliesFound > 30) throw new CloudException("Esta versão suporta até 30 casas por conta; nenhuma seleção foi feita.");
        var compatible = new List<DeviceChoice>();
        foreach(var family in list.EnumerateArray())
        {
            var id = Text(family,"familyId");
            if(string.IsNullOrEmpty(id)) continue;
            using var devices = await Api("/appsync/group/dev/query?action=select", new { pids = Array.Empty<string>() }, id);
            var endpoints = Get(Get(devices.RootElement, "data"), "endpoints");
            if(endpoints.ValueKind != JsonValueKind.Array) continue;
            DevicesFound += endpoints.GetArrayLength();
            foreach(var endpoint in endpoints.EnumerateArray())
            {
                // Product family validated against the supplied APK. Never pin a user's MAC.
                if((Text(endpoint,"productId") ?? "").EndsWith("c0620000", StringComparison.OrdinalIgnoreCase))
                {
                    var name=Text(endpoint,"friendlyName");
                    compatible.Add(new DeviceChoice(string.IsNullOrWhiteSpace(name) ? "Ar-condicionado " + (compatible.Count+1) : name,id,endpoint.Clone()));
                }
            }
        }
        AvailableDevices = compatible.AsReadOnly();
    }
    internal void SelectDevice(DeviceChoice choice)
    {
        if(!AvailableDevices.Contains(choice)) throw new CloudException("Escolha um aparelho da conta conectada.");
        TargetDevice=choice.Endpoint; targetFamily=choice.Family; SelectedDeviceName=choice.Name;
    }

    const string ApplicationLicense = "3XDE1mo3NyyAYZv0Zces+ynt4c9pj9uOlsJypHpOcBjSOa5HabWQQKAYerrsJjQNJ1uSZAAAAAALlVgzgJj8A8OWrA7xzUXurqBFXioAnREjkOs7iRLhKOu5pSKwuc4jJKk4NAM/AXlckMwxRPQVURkoz61rQzAFSOsbNs8CAqsu8HuIDs2mDQAAAAA=";
    internal Task<JsonDocument> QueryState(bool validate = true) => Control("get", Array.Empty<string>(), Array.Empty<object[]>(), validate);

    internal async Task<DeviceState> ReadState()
    {
        using var response = await QueryState();
        return DeviceState.Parse(response.RootElement);
    }

    internal async Task<DeviceState> SetValues(IReadOnlyDictionary<string,int> values)
    {
        if(values.Count == 0) throw new CloudException("Nenhum ajuste informado.");
        foreach(var value in values)
        {
            bool valid = value.Key switch {
                "pwr" or "ac_slp" or "ac_vdir" or "ac_hdir" or "ecomode" or "scrdisp" => value.Value is 0 or 1,
                "temp" => value.Value >= 160 && value.Value <= 320 && value.Value % 10 == 0,
                "ac_mode" => value.Value >= 0 && value.Value <= 4,
                "ac_mark" => value.Value >= 0 && value.Value <= 5,
                _ => false
            };
            if(!valid) throw new CloudException("Esse ajuste não é suportado pelo aparelho.");
        }
        using var response = await Control("set",values.Keys.ToArray(),values.Values.Select(value => new object[] { new { idx = 1, val = value } }).ToArray(),true);
        // Read back the physical state rather than assuming that an acknowledged write applied.
        return await ReadState();
    }

    async Task<JsonDocument> Control(string act, string[] parameters, object[][] values, bool validate)
    {
        if(TargetDevice == null) throw new CloudException("O seu ar ainda não foi identificado na conta.");
        var device = TargetDevice.Value;
        var did = Text(device,"endpointId");
        var mac = Text(device,"mac");
        var pid = Text(device,"productId");
        var cookie = Text(device,"cookie");
        int.TryParse(Text(device,"devicetypeFlag"), out var deviceFlag);
        using var cookieJson = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(cookie ?? "")));
        var key = Text(cookieJson.RootElement,"aeskey");
        int.TryParse(Text(cookieJson.RootElement,"terminalid"),out var terminalId);
        var pairedCookie = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { device = new { id = terminalId, key, aeskey = key, did, pid, mac } })));
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var directive = new {
            header = new { @namespace = "DNA.KeyValueControl", name = "KeyValueControl", timstamp = now, interfaceVersion = "2", messageId = did + "-" + now },
            endpoint = new { endpointId = did, devicePairedInfo = new { did, pid, mac, devicetypeflag = deviceFlag, cookie = pairedCookie }, cookie = new {}, devSession = Text(device,"devSession") },
            payload = new { prop = "stdctrl", act, @params = parameters, vals = values }
        };
        var response = await Api("/device/control/v2/sdkcontrol?license=" + Uri.EscapeDataString(ApplicationLicense), new { directive }, targetFamily);
        var status = Text(Get(Get(response.RootElement,"event"),"payload"),"status");
        var responsePayload = Get(Get(response.RootElement,"event"),"payload");
        if(validate && ((status != null && status != "0") || responsePayload.ValueKind != JsonValueKind.Object)) { response.Dispose(); throw new CloudException("O serviço de controle não respondeu com sucesso (código " + (status ?? "ausente") + "). O login continua conectado."); }
        return response;
    }

}
