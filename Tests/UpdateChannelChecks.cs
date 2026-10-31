using MyPiggyBank.Core;
using System.Net;
using System.Text.Json;

internal static class UpdateChannelChecks
{
    private static string Tag(int epoch,string version) => AppVersion.TagPrefix(epoch,new Version(version))+version;
    private static string Channel(int epoch=2,string version="0.5.1.1") => JsonSerializer.Serialize(new{schema=1,epoch,version,tag=(epoch>=3||epoch==2&&!version.StartsWith("0.")?"app-v":"v")+version});
    private static string Release(string version="0.5.1.1",bool draft=false,bool prerelease=false,string? url=null,bool checksum=true,int epoch=2)
    {
        var tag=Tag(epoch,version);var name=$"Piggy-Piggy-{version}-win-x64.zip";var download=$"https://github.com/Rhahee/piggy-piggy/releases/download/{tag}/{name}";
        var assets=new List<object>{new{name,size=1000,browser_download_url=url??download}};
        if(checksum)assets.Add(new{name=name+".sha256",size=100,browser_download_url=download+".sha256"});
        return JsonSerializer.Serialize(new{tag_name=tag,draft,prerelease,body="Notes",assets});
    }
    private static UpdateCheck Check(Version installed,int epoch,string channel,string release,HttpStatusCode channelStatus=HttpStatusCode.OK,HttpStatusCode releaseStatus=HttpStatusCode.OK,Action<HttpRequestMessage>? inspect=null)
    {
        using var client=new HttpClient(new Handler(request=>
        {
            inspect?.Invoke(request);
            var isChannel=request.RequestUri==ReleaseChecker.ChannelEndpoint;
            if(!isChannel&&!request.RequestUri!.AbsoluteUri.StartsWith($"https://api.github.com/repos/{ReleaseChecker.Repository}/releases/",StringComparison.Ordinal))throw new Exception("Unexpected request");
            return new(isChannel?channelStatus:releaseStatus){Content=new StringContent(isChannel?channel:release)};
        }));
        return new ReleaseChecker(client).CheckChannelAsync(installed,epoch).GetAwaiter().GetResult();
    }
    private static void Equal<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}, got {actual}");}
    private static void Reject(string json){try{UpdateChannel.Parse(json);}catch(Exception e)when(e is InvalidDataException or JsonException){return;}throw new Exception("Expected invalid channel rejection");}
    public static void Run(Action<string,Action> test)
    {
        test("0.1.0.1 offers the complete 0.1.0.2 package within epoch 3",()=>{
            var result=Check(new(0,1,0,1),3,Channel(3,"0.1.0.2"),Release("0.1.0.2",epoch:3));
            Equal(UpdateStatus.Available,result.Status);Equal(new Version(0,1,0,2),result.Release!.Version);
            Equal(false,result.Release.IsVersionTransition);Equal(true,result.Release.Checksum is not null);
        });
        test("0.1.0.2 does not offer itself or published 0.1.0.1 as an update",()=>{
            foreach(var version in new[]{"0.1.0.1","0.1.0.2"})
                Equal(UpdateStatus.UpToDate,Check(new(0,1,0,2),3,Channel(3,version),Release(version,epoch:3)).Status);
        });
        var bridge=new Version(1,5,1,2);var baseline=new Version(0,5,1,1);
        test("channel parser validates schema epoch and exact four-part version tag",()=>{var c=UpdateChannel.Parse(Channel());Equal(2,c.Epoch);Equal(baseline,c.Version);Equal("v0.5.1.1",c.Tag);});
        test("channel parser rejects unsupported malformed and oversized metadata",()=>
        {
            foreach(var value in new[]{"null","[]","{}","not json",Channel().Replace("\"schema\":1","\"schema\":2"),Channel().Replace("\"epoch\":2","\"epoch\":4"),Channel().Replace("\"epoch\":2","\"epoch\":\"2\""),Channel().Replace("\"epoch\":2","\"epoch\":0"),Channel().Replace("v0.5.1.1","../latest"),Channel().Replace("v0.5.1.1","v0.5.1.2"),Channel(version:"0.5.1"),Channel(version:"0.5.1.1-beta"),Channel(version:"9999999999999999999.0.0.0"),new string(' ',4097)})Reject(value);
        });
        test("bridge can offer a numerically lower version only across a validated newer epoch",()=>{var r=Check(bridge,1,Channel(),Release());Equal(UpdateStatus.Available,r.Status);Equal(baseline,r.Release!.Version);Equal(true,r.Release.IsVersionTransition);Equal(true,r.Release.Checksum is not null);});
        test("same epoch keeps numeric downgrade protection",()=>{var r=Check(new(0,5,1,2),2,Channel(),Release());Equal(UpdateStatus.UpToDate,r.Status);Equal<ReleaseInfo?>(null,r.Release);});
        test("future stable tags cannot collide with legacy one-series releases",()=>{Reject(Channel(version:"1.0.0.0").Replace("app-v1.0.0.0","v1.0.0.0"));Equal("app-v1.0.0.0",UpdateChannel.Parse(Channel(version:"1.0.0.0")).Tag);});
        test("baseline is current and does not return to legacy bridge",()=>Equal(UpdateStatus.UpToDate,Check(baseline,2,Channel(),Release()).Status));
        test("normal zero-series updates and future one-series releases stay in the new epoch",()=>
        {
            foreach(var v in new[]{"0.5.1.2","0.6.0.0","1.0.0.0","1.5.1.1","2.0.0.0"}){var r=Check(baseline,2,Channel(version:v),Release(v));Equal(UpdateStatus.Available,r.Status);Equal(false,r.Release!.IsVersionTransition);}
        });
        test("new epoch rejects an old epoch channel without falling back",()=>{var requests=0;Equal(UpdateStatus.Unavailable,Check(baseline,2,Channel(1,"1.5.1.2"),Release("1.5.1.2"),inspect:_=>requests++).Status);Equal(1,requests);});
        test("unknown installed epoch cannot authorize a transition",()=>Equal(UpdateStatus.Unavailable,Check(bridge,4,Channel(),Release()).Status));
        test("bridge can use legacy latest while channel is absent",()=>Equal(UpdateStatus.Available,Check(new(1,5,1,1),1,"",Release("1.5.1.2",epoch:1),HttpStatusCode.NotFound).Status));
        test("new epoch never falls back to legacy latest when channel is absent",()=>{var requests=0;Equal(UpdateStatus.Unavailable,Check(baseline,2,"",Release("1.5.1.2"),HttpStatusCode.NotFound,inspect:_=>requests++).Status);Equal(1,requests);});
        test("channel targets an exact release rather than global latest and sends no user data",()=>
        {
            var urls=new List<Uri>();Check(bridge,1,Channel(),Release(),inspect:r=>{urls.Add(r.RequestUri!);Equal(HttpMethod.Get,r.Method);Equal(true,r.Headers.Authorization is null&&r.Content is null);Equal(true,r.Headers.CacheControl!.NoCache);});
            Equal(2,urls.Count);Equal(ReleaseChecker.ChannelEndpoint,urls[0]);Equal($"https://api.github.com/repos/{ReleaseChecker.Repository}/releases/tags/v0.5.1.1",urls[1].AbsoluteUri);
        });
        test("channel release identity mismatch is rejected",()=>Equal(UpdateStatus.Unavailable,Check(bridge,1,Channel(),Release("0.5.1.2")).Status));
        test("transition still rejects draft and prerelease targets",()=>{Equal(UpdateStatus.Unavailable,Check(bridge,1,Channel(),Release(draft:true)).Status);Equal(UpdateStatus.Unavailable,Check(bridge,1,Channel(),Release(prerelease:true)).Status);});
        test("transition still validates repository download links",()=>Equal(UpdateStatus.Unavailable,Check(bridge,1,Channel(),Release(url:"https://example.com/app.zip")).Status));
        test("missing transition checksum cannot enable automatic installation",()=>Equal<Uri?>(null,Check(bridge,1,Channel(),Release(checksum:false)).Release!.Checksum));
        test("unpublished or removed target remains unavailable",()=>Equal(UpdateStatus.Unavailable,Check(bridge,1,Channel(),Release(),releaseStatus:HttpStatusCode.NotFound).Status));
        test("HTTP errors invalid metadata and timeout cannot authorize a transition",()=>
        {
            foreach(var status in new[]{HttpStatusCode.Forbidden,HttpStatusCode.TooManyRequests,HttpStatusCode.InternalServerError})Equal(UpdateStatus.Unavailable,Check(bridge,1,Channel(),Release(),status).Status);
            Equal(UpdateStatus.Unavailable,Check(bridge,1,"{}",Release()).Status);
            using var client=new HttpClient(new Handler(_=>throw new TaskCanceledException()));Equal(UpdateStatus.Unavailable,new ReleaseChecker(client).CheckChannelAsync(bridge,1).GetAwaiter().GetResult().Status);
        });
        test("caller cancellation is preserved",()=>
        {
            using var source=new CancellationTokenSource();source.Cancel();using var client=new HttpClient(new Handler(_=>throw new OperationCanceledException(source.Token)));
            try{new ReleaseChecker(client).CheckChannelAsync(bridge,1,source.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){return;}throw new Exception("Cancellation swallowed");
        });
        test("initial 0.1 baseline uses a separate tag namespace and is current",()=>
        {
            var initial=new Version(0,1,0,0);var c=UpdateChannel.Parse(Channel(3,"0.1.0.0"));Equal(3,c.Epoch);Equal("app-v0.1.0.0",c.Tag);
            Equal(UpdateStatus.UpToDate,Check(initial,3,Channel(3,"0.1.0.0"),Release("0.1.0.0",epoch:3)).Status);
            Reject(Channel(3,"0.1.0.0").Replace("app-v0.1.0.0","v0.1.0.0"));
        });
        test("initial baseline never returns to either experimental numbering generation",()=>
        {
            foreach(var e in new[]{1,2}){var calls=0;Equal(UpdateStatus.Unavailable,Check(new(0,1,0,0),3,Channel(e,e==1?"1.5.1.2":"0.5.1.1"),Release(),inspect:_=>calls++).Status);Equal(1,calls);}
        });
        test("future initial-baseline updates remain numerically ordered within generation three",()=>
        {
            foreach(var v in new[]{"0.1.0.1","0.1.1.0","0.2.0.0","0.5.1.1","1.0.0.0"}){var r=Check(new(0,1,0,0),3,Channel(3,v),Release(v,epoch:3));Equal(UpdateStatus.Available,r.Status);Equal(false,r.Release!.IsVersionTransition);Equal(true,r.Release.Page.AbsoluteUri.Contains("/app-v"+v));}
            Equal(UpdateStatus.UpToDate,Check(new(0,1,0,1),3,Channel(3,"0.1.0.0"),Release("0.1.0.0",epoch:3)).Status);
        });
    }
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(respond(request));
    }
}
