using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// Static reconstruction of BLAccount.login, account HTTP accessor and BLCommonTools.
// Hosts/lid come from res/xml/app_servers.xml in the supplied APK.
internal sealed partial class ElginCloud : IDisposable
{
    internal const string LicenseId = "dd70c4d66a37372c80619bf465c7acfb";
    // networkapi_license_info reads the company UID from license bytes 0x78..0x87.
    internal const string CompanyId = "48eb1b36cf0202ab2ef07b880ecda60d";
    static readonly byte[] Iv = Convert.FromHexString("eaaaaa3abb5862a21918b5771d1615aa");
    static readonly string[] Hosts = {
        "app-service-chn-31a93883.ibroadlink.com",
        "app-service-usa-fd7cc04c.smarthomecs.com",
        "app-service-deu-f0e9ebbb.smarthomecs.de"
    };
    readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(25) };
    internal string? UserId { get; private set; }
    internal string? Session { get; private set; }
    int currentRegion;
    internal bool IsAuthenticated => !string.IsNullOrEmpty(UserId) && !string.IsNullOrEmpty(Session);
    internal void SignOut() { UserId = null; Session = null; TargetDevice = null; targetFamily = null; AvailableDevices = Array.Empty<DeviceChoice>(); SelectedDeviceName = null; }

    internal static string PasswordHash(string password) => Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(password + "4969fj#k23#")));
    internal static (byte[] Body, string Token) Encode(string json, long timestamp)
    {
        var source = Encoding.UTF8.GetBytes(json);
        // APK first aligns manually, then its ZeroBytePadding provider adds a full block.
        var padded = new byte[(source.Length + 15) / 16 * 16 + 16];
        source.CopyTo(padded, 0);
        var key = MD5.HashData(Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + "kdixkdqp54545^#*"));
        try
        {
            using var aes = Aes.Create();
            aes.Key = key;
            var encrypted = aes.EncryptCbc(padded, Iv, PaddingMode.None);
            var token = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(json + "xgx3d*fe3478$ukx")));
            return (encrypted, token);
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(source); CryptographicOperations.ZeroMemory(padded); }
    }

    internal async Task Login(string username, string password, int region, CancellationToken cancellationToken = default)
    {
        SignOut();
        username = username.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) throw new CloudException("Preencha sua conta e sua senha.");
        if(username.Contains('\r') || username.Contains('\n')) throw new CloudException("Confira o e-mail ou telefone informado.");
        var fields = new Dictionary<string, object>();
        bool email = username.Contains('@');
        fields[email ? "email" : "phone"] = username;
        fields["password"] = PasswordHash(password);
        fields["companyid"] = CompanyId;
        fields["lid"] = LicenseId;
        var json = JsonSerializer.Serialize(fields, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        using var response = await Request(json, region, email ? "email" : "phone", username, cancellationToken);
        var root = response.RootElement;
        if(root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Number || !error.TryGetInt32(out var number)) throw new CloudException("O servidor retornou um formato inesperado. A conexão ainda precisa ser ajustada.");
        if(number != 0) throw new CloudException($"O Elgin Air não aceitou o login (código {number}). Confira a conta, a senha e a região usada no celular.", number);
        var userId = root.TryGetProperty("userid", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
        var session = root.TryGetProperty("loginsession", out var token) && token.ValueKind == JsonValueKind.String ? token.GetString() : null;
        if(string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(session)) throw new CloudException("O servidor não retornou uma sessão utilizável. Nenhuma conexão foi indicada como concluída.");
        UserId = userId; Session = session; currentRegion = region;
    }

    async Task<JsonDocument> Request(string json, int region, string? usernameKind, string? username, CancellationToken cancellationToken)
    {
        if(region < 0 || region >= Hosts.Length) throw new CloudException("Selecione a região da conta.");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(http.Timeout);
        cancellationToken=deadline.Token;
        var now = DateTimeOffset.UtcNow;
        var timestamp = now.ToUnixTimeSeconds();
        var encoded = Encode(json, timestamp);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"https://{Hosts[region]}/account/login"));
        request.Content = new ByteArrayContent(encoded.Body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-java-serialized-object");
        request.Headers.Add("timestamp", timestamp.ToString(CultureInfo.InvariantCulture));
        request.Headers.Add("token", encoded.Token);
        request.Headers.Add("lid", LicenseId);
        request.Headers.Add("licenseId", LicenseId);
        request.Headers.Add("system", "android");
        request.Headers.Add("appPlatform", "android");
        request.Headers.Add("language", "pt-BR");
        request.Headers.Add("appVersion", "1.0.1_2238f981e");
        request.Headers.Add("messageId", now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
        // Matches the APK's mutually exclusive mode; a successful login may sign out the phone.
        request.Headers.Add("loginmode", "mutuallyexclusive");
        if(usernameKind != null && username != null) request.Headers.Add(usernameKind, username);
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if((int)response.StatusCode >= 300 && (int)response.StatusCode < 400) throw new CloudException("O servidor solicitou redirecionamento. O login foi interrompido para conferir o destino.");
            if(!response.IsSuccessStatusCode) throw new CloudException("O servidor não aceitou a conexão de login (HTTP " + (int)response.StatusCode + ").");
            if(response.Content.Headers.ContentLength > 65536) throw new CloudException("Resposta inesperada do servidor.");
            await response.Content.LoadIntoBufferAsync(65536, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            try { return JsonDocument.Parse(text); }
            catch(JsonException) { throw new CloudException($"O servidor não retornou JSON (HTTP {(int)response.StatusCode})."); }
        }
        catch(HttpRequestException) { throw new CloudException("Não foi possível conversar com o servidor Elgin Air. Confira a internet e tente novamente."); }
        catch(OperationCanceledException) { throw new CloudException("A conexão demorou demais ou foi cancelada. Tente novamente."); }
        finally { CryptographicOperations.ZeroMemory(encoded.Body); }
    }

    // Anonymous diagnostic: empty JSON, with no username/password and no session.
    internal async Task<(int? Error, string? Message)> Probe()
    {
        using var response = await Request("{}", 0, null, null, CancellationToken.None);
        return (response.RootElement.TryGetProperty("error", out var error) && error.TryGetInt32(out var code) ? code : null,
            response.RootElement.TryGetProperty("msg", out var msg) ? msg.GetString() : null);
    }
    public void Dispose() { SignOut(); http.Dispose(); }
}

internal sealed class CloudException : Exception
{
    internal int? ErrorCode { get; }
    internal CloudException(string message, int? errorCode = null) : base(message) { ErrorCode = errorCode; }
}
