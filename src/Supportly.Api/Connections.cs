using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
class Connections
{
    readonly ConcurrentDictionary<string,(string User,string Token,HubCallerContext Context)> active=new();
    public void Add(string user,string token,HubCallerContext context)=>active[context.ConnectionId]=(user,token,context);
    public void Remove(string id)=>active.TryRemove(id,out _);
    public void AbortUser(string id){foreach(var item in active.Values)if(item.User==id)item.Context.Abort();}
    public void AbortToken(string token){foreach(var item in active.Values)if(item.Token==token)item.Context.Abort();}
}
