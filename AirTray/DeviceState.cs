using System;
using System.Collections.Generic;
using System.Text.Json;

internal sealed record DeviceState(IReadOnlyDictionary<string,int> Values, DateTimeOffset Updated)
{
    internal bool Has(string property) => Values.ContainsKey(property);
    internal int Value(string property) => Values.TryGetValue(property,out var value) ? value : throw new CloudException("O aparelho não informou " + property + ".");
    internal bool Power => Value("pwr") == 1;
    internal double Temperature => Value("temp") / 10d;
    static bool Property(JsonElement element,string name,out JsonElement value)
    {
        value=default;
        return element.ValueKind==JsonValueKind.Object && element.TryGetProperty(name,out value);
    }
    internal static DeviceState Parse(JsonElement response)
    {
        JsonDocument? decoded=null;
        try
        {
            if(!Property(response,"event",out var ev) || !Property(ev,"payload",out var payload) || !Property(payload,"data",out var data)) throw new CloudException("O aparelho não retornou um estado válido.");
            if(data.ValueKind==JsonValueKind.String) { decoded=JsonDocument.Parse(data.GetString()!); data=decoded.RootElement; }
            if(!Property(data,"params",out var names) || !Property(data,"vals",out var values) || names.ValueKind!=JsonValueKind.Array || values.ValueKind!=JsonValueKind.Array || names.GetArrayLength()!=values.GetArrayLength()) throw new CloudException("O estado retornado pelo aparelho está incompleto.");
            var result=new Dictionary<string,int>(StringComparer.Ordinal);
            for(int i=0;i<names.GetArrayLength();i++)
            {
                if(names[i].ValueKind!=JsonValueKind.String || values[i].ValueKind!=JsonValueKind.Array) throw new CloudException("O aparelho retornou propriedades em formato inválido.");
                var name=names[i].GetString()!;
                foreach(var item in values[i].EnumerateArray())
                {
                    if(!Property(item,"idx",out var idx) || idx.ValueKind!=JsonValueKind.Number || !idx.TryGetInt32(out var index) || index!=1) continue;
                    if(Property(item,"val",out var value) && value.ValueKind==JsonValueKind.Number && value.TryGetInt32(out var number))
                    {
                        if(!result.TryAdd(name,number)) throw new CloudException("O aparelho retornou propriedades duplicadas.");
                        break;
                    }
                }
            }
            if(!result.TryGetValue("temp",out var temperature) || !result.TryGetValue("pwr",out var power) || power is not (0 or 1)) throw new CloudException("O aparelho não confirmou energia e temperatura válidas.");
            if((!result.TryGetValue("tempunit",out var unit) || unit==1) && temperature is <160 or >320) throw new CloudException("A temperatura retornada está fora da faixa suportada.");
            return new DeviceState(result,DateTimeOffset.Now);
        }
        catch(JsonException) { throw new CloudException("O aparelho retornou um estado em formato inválido."); }
        finally { decoded?.Dispose(); }
    }
}
