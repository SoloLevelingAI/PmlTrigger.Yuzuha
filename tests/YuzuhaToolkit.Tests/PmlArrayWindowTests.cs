using Xunit;
using YuzuhaToolkit.Pml;
using YuzuhaToolkit.Mcp;

namespace YuzuhaToolkit.Tests;

public sealed class PmlArrayWindowTests
{
    [Fact]
    public void ItemBudgetLimitsHostReadsAndReportsContinuation()
    {
        var reads=new List<int>();
        var result=PmlArrayWindow.Read(1000000,1,3,100,i=>{reads.Add(i);return "row"+i;});
        Assert.Equal(new[]{1,2,3},reads);Assert.Equal(4,result.NextIndex);Assert.True(result.Truncated);
        Assert.Equal(1000000,result.IndexUpperBound);Assert.Equal(3,result.Rows.Length);
    }
    [Fact]
    public void CharacterBudgetNeverSplitsRowsAndPreservesNextIndex()
    {
        var result=PmlArrayWindow.Read(5,1,5,6,i=>"abcd");
        Assert.Single(result.Rows);Assert.Equal(2,result.NextIndex);Assert.Equal(4,result.Characters);
        Assert.Throws<InvalidOperationException>(()=>PmlArrayWindow.Read(1,1,1,3,_=>"long"));
    }
    [Fact]
    public void EmptyAndPastEndDoNotReadOrOverflow()
    {
        Assert.Empty(PmlArrayWindow.Read(0,1,2,10,_=>throw new Exception()).Rows);
        Assert.Empty(PmlArrayWindow.Read(3,4,2,10,_=>throw new Exception()).Rows);
        var last=PmlArrayWindow.Read(int.MaxValue,int.MaxValue,1,10,_=>"end");
        Assert.False(last.Truncated);Assert.Equal(0,last.NextIndex);
    }
    [Theory]
    [InlineData(0,1,10)]
    [InlineData(1,0,10)]
    [InlineData(1,1001,10)]
    [InlineData(1,1,0)]
    [InlineData(1,1,262145)]
    public void InvalidBudgetsFailBeforeAnyRead(int start,int items,int chars) =>
        Assert.Throws<ArgumentOutOfRangeException>(()=>PmlArrayWindow.Read(10,start,items,chars,_=>throw new Exception("must not read")));

    [Fact]
    public async Task OlderHostIsRejectedBeforeAnyCommandIsSent()
    {
        var service=new FakeService();
        var error = await Assert.ThrowsAsync<HostCapabilityException>(()=>BoundedArrayRequest.SendAsync(service,new(){BoundedArrayReadVersion=0},"Sample",1,10,100,default));
        Assert.Equal("PML_BOUNDED_ARRAY_UNSUPPORTED", error.Code);
        Assert.Equal(0,service.Calls);
    }
    [Fact]
    public async Task CapableHostReceivesBoundsAndNoExpression()
    {
        var service=new FakeService();
        await BoundedArrayRequest.SendAsync(service,new(){BoundedArrayReadVersion=1},"Sample",2,10,100,default);
        Assert.True(service.Request!.ReadArray);Assert.Null(service.Request.PmlCommand);
        Assert.Equal(2,service.Request.StartIndex);Assert.Equal(10,service.Request.MaxItems);Assert.Equal(100,service.Request.MaxCharacters);
    }
    private sealed class FakeService : YuzuhaToolkit.Mcp.IPmlCommandService
    {
        public int Calls;public YuzuhaToolkit.Mcp.RunPmlCommandRequest? Request;
        public Task<YuzuhaToolkit.Mcp.HostIdentityResponse> GetHostIdentityAsync(YuzuhaToolkit.Mcp.HostIdentityRequest request,CancellationToken token)=>throw new NotSupportedException();
        public Task<YuzuhaToolkit.Mcp.RunPmlCommandResponse> RunPmlCommandAsync(YuzuhaToolkit.Mcp.RunPmlCommandRequest request,CancellationToken token){Calls++;Request=request;return Task.FromResult(new YuzuhaToolkit.Mcp.RunPmlCommandResponse{Success=true});}
    }
}
