using System;
using System.Text.Json;

internal static class DeviceStateTests
{
    static object Data(int power,int temperature) => new { @params=new[]{"pwr","temp"}, vals=new[]{new[]{new {idx=1,val=power}},new[]{new {idx=1,val=temperature}}} };
    static string Wrap(object data) => JsonSerializer.Serialize(new { @event=new {payload=new {data}} });
    static DeviceState Parse(string json) { using var document=JsonDocument.Parse(json); return DeviceState.Parse(document.RootElement); }
    internal static void Run()
    {
        foreach(var temperature in new[]{160,260,320})
        {
            var state=Parse(Wrap(Data(1,temperature)));
            if(!state.Power || state.Temperature!=temperature/10d) throw new InvalidOperationException("Valid state was rejected.");
        }
        if(Parse(Wrap(JsonSerializer.Serialize(Data(0,260)))).Power) throw new InvalidOperationException("String-encoded state was not parsed.");
        foreach(var json in new[]{"[]","null","{}",Wrap(new object[0]),Wrap("not-json"),Wrap(Data(2,260)),Wrap(Data(1,159)),Wrap(Data(1,321)),
            Wrap(new { @params=new[]{"temp"}, vals=Array.Empty<object>() }),
            Wrap(new { @params=new object[]{4,"temp"}, vals=new[]{new[]{new {idx=1,val=0}},new[]{new {idx=1,val=260}}} }),
            Wrap(new { @params=new[]{"pwr","temp"}, vals=new[]{new[]{new {idx="wrong",val=0}},new[]{new {idx="wrong",val=260}}} }),
            Wrap(new { @params=new[]{"pwr","temp","pwr"}, vals=new[]{new[]{new {idx=1,val=0}},new[]{new {idx=1,val=260}},new[]{new {idx=1,val=1}}} })})
        {
            try { Parse(json); } catch(CloudException) { continue; }
            throw new InvalidOperationException("Malformed state was accepted.");
        }
        using var cloud=new ElginCloud(); using var endpoint=JsonDocument.Parse("{}");
        try { cloud.SelectDevice(new ElginCloud.DeviceChoice("Fixture","fixture",endpoint.RootElement)); }
        catch(CloudException) { Console.WriteLine("PASS: valid/string states, temperature bounds, malformed responses, invalid power/index, duplicate fields and rejecting a device outside the account catalog. Offline tests only."); return; }
        throw new InvalidOperationException("Unlisted device was accepted.");
    }
}
