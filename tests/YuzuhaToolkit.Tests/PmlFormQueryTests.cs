using System.Text.RegularExpressions;
using Xunit;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.Tests;

public sealed class PmlFormQueryTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void NativeTemporariesUseCanonicalNamesAndPreserveScalarTypes(bool canonicalNames, bool fixedTypes)
    {
        var runtime = new FakeRuntime { EnforceCanonicalNames = canonicalNames, EnforceScalarTypes = fixedTypes };
        var result = PmlFormQuery.Read(Request("forms"), runtime);
        Assert.Equal(2, result.Items.Length);
        Assert.Equal(2, result.IndexUpperBound);
    }

    [Fact]
    public void SparseNativeSlotsConsumeBudgetWithoutReadingMissingValues()
    {
        var runtime = new FakeRuntime { Upper = 3, MissingSlot = 2 };
        var request = Request("forms");
        var page = PmlFormQuery.Read(request, runtime);
        Assert.Equal("Form1", Assert.Single(page.Items).Name);
        Assert.Equal(2, page.Scanned); Assert.Equal(3, page.NextIndex);
        request.StartIndex = page.NextIndex;
        Assert.Equal("Form3", Assert.Single(PmlFormQuery.Read(request, runtime).Items).Name);
    }

    [Fact]
    public void UnsetGadgetReferencesAreSkippedInListsAndFailDetails()
    {
        var runtime = new FakeRuntime { Upper = 4, GadgetSet = false };
        var request = Request("gadgets"); request.MaxItems = 3;
        var page = PmlFormQuery.Read(request, runtime);
        Assert.Empty(page.Items); Assert.Equal(3, page.Scanned); Assert.Equal(4, page.NextIndex);
        request = Request("gadget"); request.GadgetName = "accept";
        Assert.Contains("PML_UI_GADGET_UNSET", Assert.Throws<InvalidOperationException>(() => PmlFormQuery.Read(request, runtime)).Message);
    }

    [Theory]
    [InlineData("shown-forms", "!!FMSYS.SHOWNFORMS()")]
    [InlineData("forms", "!!FMSYS.FORMS()")]
    public void FormListsUseNativeManagerAndHonorScanBudget(string kind, string expression)
    {
        var runtime = new FakeRuntime { Upper = 5 };
        var result = PmlFormQuery.Read(Request(kind), runtime);
        Assert.Contains(runtime.Commands, command => command.EndsWith(" = " + expression));
        Assert.Equal(new[] { "Form1", "Form2" }, result.Items.Select(item => item.Name));
        Assert.Equal("!!Form1", result.Items[0].Reference);
        Assert.Equal(3, result.NextIndex);
        Assert.Equal(2, result.Scanned);
        Assert.True(result.Truncated);
        Assert.Equal(5, result.IndexUpperBound);
        Assert.Equal(2, runtime.Commands.Count(command => command.EndsWith(".name()")));
        AssertOnlyRequestTemporariesAreDeleted(runtime);
    }

    [Fact]
    public void GadgetListUsesNativeAttributeTypesAndSkipsOrdinaryMembers()
    {
        var runtime = new FakeRuntime { Upper = 4 };
        var request = Request("gadgets"); request.MaxItems = 3;
        var result = PmlFormQuery.Read(request, runtime);
        Assert.Contains(runtime.Commands, command => command.EndsWith(" = !!Sample.attributes()"));
        var item = Assert.Single(result.Items);
        Assert.Equal("accept", item.Name);
        Assert.Equal("BUTTON", item.Type);
        Assert.Equal("!!Sample.accept", item.Reference);
        Assert.Equal(4, result.NextIndex);
        Assert.Equal(3, result.Scanned);
        Assert.Contains(runtime.Commands, command => command.Contains(".attribute(") && command.EndsWith(".objectType()"));
        AssertOnlyRequestTemporariesAreDeleted(runtime);
    }

    [Fact]
    public void GadgetDetailsReadMetadataWithoutInvokingCallbacksOrValues()
    {
        var runtime = new FakeRuntime();
        var request = Request("gadget"); request.GadgetName = "accept";
        var result = PmlFormQuery.Read(request, runtime);
        var item = Assert.Single(result.Items);
        Assert.Equal("accept", item.Name); Assert.Equal("BUTTON", item.Type);
        Assert.Contains(item.Properties, property => property.Name == "shown" && property.Value == "TRUE" && property.Status == "available");
        Assert.Contains(item.Properties, property => property.Name == "callback" && property.Value == "!!SideEffect()" && property.Status == "available");
        Assert.DoesNotContain(runtime.Commands, command => command.Contains(".val") || command.EndsWith(".callback()") || command.Contains(" = !!SideEffect()"));
        AssertOnlyRequestTemporariesAreDeleted(runtime);
    }

    [Fact]
    public void UnsupportedMetadataIsExplicitAndRequiredReadFailuresFailTheQuery()
    {
        var runtime = new FakeRuntime { FailOn = ".tooltip" };
        var request = Request("gadget"); request.GadgetName = "accept";
        var item = Assert.Single(PmlFormQuery.Read(request, runtime).Items);
        var unavailable = Assert.Single(item.Properties, property => property.Name == "tooltip");
        Assert.Equal("unavailable", unavailable.Status); Assert.Null(unavailable.Value);
        Assert.Contains("native read failed", unavailable.Error);
        runtime = new FakeRuntime { FailOn = ".type()" };
        Assert.Throws<InvalidOperationException>(() => PmlFormQuery.Read(request, runtime));
        AssertOnlyRequestTemporariesAreDeleted(runtime);
    }

    [Fact]
    public void MissingOrNonGadgetDoesNotBecomeAnEmptySuccessfulDetail()
    {
        var request = Request("gadget"); request.GadgetName = "accept";
        var runtime = new FakeRuntime { DetailObjectType = "STRING" };
        Assert.Throws<InvalidOperationException>(() => PmlFormQuery.Read(request, runtime));
        AssertOnlyRequestTemporariesAreDeleted(runtime);
    }

    [Fact]
    public void NonFormGlobalsAreRejectedBeforeEnumeratingMembers()
    {
        var runtime = new FakeRuntime { FormObjectType = "ARRAY" };
        Assert.Throws<InvalidOperationException>(() => PmlFormQuery.Read(Request("gadgets"), runtime));
        Assert.DoesNotContain(runtime.Commands, command => command.EndsWith(".attributes()"));
        AssertOnlyRequestTemporariesAreDeleted(runtime);
    }

    [Fact]
    public void CleanupFailureFailsAndStillAttemptsBothTemporaries()
    {
        var runtime = new FakeRuntime { FailOn = ".delete()" };
        Assert.Throws<InvalidOperationException>(() => PmlFormQuery.Read(Request("forms"), runtime));
        AssertOnlyRequestTemporariesAreDeleted(runtime);
    }

    [Fact]
    public void GadgetReferenceStaysInsideTemporaryArrayDuringCleanup()
    {
        var runtime = new FakeRuntime();
        var request = Request("gadget"); request.GadgetName = "accept";
        PmlFormQuery.Read(request, runtime);
        Assert.Contains(runtime.Commands, command => Regex.IsMatch(command, @"^!!YUZUHAUISNAPSHOT[A-F0-9]{32}\[1\] = !!Sample\.attribute\('accept'\)$"));
        Assert.DoesNotContain(runtime.Commands, command => Regex.IsMatch(command, @"^!!YUZUHAUISNAPSHOT[A-F0-9]{32} = !!Sample\.attribute"));
        AssertOnlyRequestTemporariesAreDeleted(runtime);
    }

    [Fact]
    public void CharacterBudgetKeepsWholeItemsAndContinuation()
    {
        var request = Request("forms"); request.MaxCharacters = 22;
        var result = PmlFormQuery.Read(request, new FakeRuntime { Upper = 5 });
        Assert.Single(result.Items); Assert.Equal(2, result.NextIndex);
        Assert.True(result.Characters <= request.MaxCharacters);
        request.MaxCharacters = 1;
        var runtime = new FakeRuntime();
        Assert.Throws<InvalidOperationException>(() => PmlFormQuery.Read(request, runtime));
        AssertOnlyRequestTemporariesAreDeleted(runtime);
    }

    [Theory]
    [InlineData("!!Sample")]
    [InlineData("Sample.accept")]
    [InlineData("Sample\nKILL ALL")]
    [InlineData("Sample'Other")]
    [InlineData("Sample;show")]
    [InlineData("$Sample")]
    public void InvalidNamesFailBeforeNativeAccess(string name)
    {
        var runtime = new FakeRuntime();
        var request = Request("gadgets"); request.FormName = name;
        Assert.Throws<ArgumentException>(() => PmlFormQuery.Read(request, runtime));
        Assert.Empty(runtime.Commands);
        request.FormName = "Sample"; request.Kind = "gadget"; request.GadgetName = name;
        Assert.Throws<ArgumentException>(() => PmlFormQuery.Read(request, runtime));
        Assert.Empty(runtime.Commands);
    }

    [Fact]
    public void InvalidBudgetsKindsAndMixedOperationsFailBeforeNativeAccess()
    {
        var runtime = new FakeRuntime();
        var request = Request("forms"); request.StartIndex = 0;
        Assert.Throws<ArgumentOutOfRangeException>(() => PmlFormQuery.Read(request, runtime));
        request.StartIndex = 1; request.Kind = "loadform";
        Assert.Throws<ArgumentException>(() => PmlFormQuery.Read(request, runtime));
        request.Kind = "forms"; request.FormName = "Sample";
        Assert.Throws<ArgumentException>(() => PmlFormQuery.Read(request, runtime));
        request.FormName = null;
        Assert.Throws<ArgumentException>(() => PmlFormQuery.ValidateEnvelope(true, false, null, null, false));
        Assert.Throws<ArgumentException>(() => PmlFormQuery.ValidateEnvelope(false, false, "show !!Sample", null, false));
        Assert.Throws<ArgumentException>(() => PmlFormQuery.ValidateEnvelope(false, true, null, null, false));
        Assert.Throws<ArgumentException>(() => PmlFormQuery.ValidateEnvelope(false, false, null, "Other", false));
        Assert.Throws<ArgumentException>(() => PmlFormQuery.ValidateEnvelope(false, false, null, null, true));
        Assert.Empty(runtime.Commands);
    }

    [Fact]
    public void EmptyAndPastEndAvoidItemReadsAndCleanupOnNativeFailure()
    {
        Assert.Empty(PmlFormQuery.Read(Request("forms"), new FakeRuntime { Upper = 0 }).Items);
        var request = Request("forms"); request.StartIndex = 10;
        var runtime = new FakeRuntime { Upper = 3 };
        Assert.Empty(PmlFormQuery.Read(request, runtime).Items);
        Assert.DoesNotContain(runtime.Commands, command => command.EndsWith(".name()"));
        runtime = new FakeRuntime { FailOn = ".MAXINDEX()" };
        Assert.Throws<InvalidOperationException>(() => PmlFormQuery.Read(Request("forms"), runtime));
        AssertOnlyRequestTemporariesAreDeleted(runtime);
    }

    [Fact]
    public async Task OldHostIsRejectedAndCapableHostReceivesOnlyTypedQuery()
    {
        var service = new FakeService();
        await Assert.ThrowsAsync<NotSupportedException>(() => PmlFormQueryRequestSender.SendAsync(service, new(), Request("forms"), default));
        Assert.Null(service.Request);
        await PmlFormQueryRequestSender.SendAsync(service, new() { FormQueryVersion = 1 }, Request("forms"), default);
        Assert.NotNull(service.Request!.FormQuery); Assert.Null(service.Request.PmlCommand);
        Assert.Null(service.Request.GlobalVar); Assert.False(service.Request.ReadArray); Assert.False(service.Request.ReturnList);
    }

    private static PmlFormQueryRequest Request(string kind) => new()
    {
        Kind = kind, FormName = kind is "gadgets" or "gadget" ? "Sample" : null,
        StartIndex = 1, MaxItems = 2, MaxCharacters = 4096
    };

    private static void AssertOnlyRequestTemporariesAreDeleted(FakeRuntime runtime)
    {
        var deletes = runtime.Commands.Where(command => command.EndsWith(".delete()")).ToArray();
        Assert.Equal(4, deletes.Length);
        Assert.All(deletes, command => Assert.Matches(@"^!!YUZUHAUI[A-Z]+[A-F0-9]{32}\.delete\(\)$", command));
        Assert.DoesNotContain(runtime.Commands, command => Regex.IsMatch(command, @"(?i)\b(show|hide|kill|loadform)\b"));
    }

    private sealed class FakeRuntime : IPmlFormQueryRuntime
    {
        public readonly List<string> Commands = [];
        public int Upper = 2;
        public string? FailOn;
        public string DetailObjectType = "GADGET";
        public string FormObjectType = "FORM";
        public bool EnforceCanonicalNames;
        public bool EnforceScalarTypes;
        public int MissingSlot;
        public bool GadgetSet = true;
        private string definedValue = "TRUE";
        private readonly Dictionary<string, string> scalarTypes = new();
        private string expression = "";
        public void Execute(string command)
        {
            Commands.Add(command);
            var assignment = Regex.Match(command, @"^!!([A-Za-z0-9_]+) = (.*)$");
            if (assignment.Success)
            {
                var name = assignment.Groups[1].Value;
                if (EnforceCanonicalNames && name != name.ToUpperInvariant())
                    throw new InvalidOperationException("Native getters require canonical uppercase variable names.");
                var source = assignment.Groups[2].Value;
                if (source.StartsWith("DEFINED("))
                {
                    var definedIndex = Regex.Match(source, @"\[(\d+)\]");
                    definedValue = definedIndex.Success && int.Parse(definedIndex.Groups[1].Value) == MissingSlot ? "FALSE" : "TRUE";
                }
                var type = source == "''" ? "STRING" : source == "0" || source.EndsWith(".MAXINDEX()") ? "REAL" : null;
                if (type is not null)
                {
                    if (EnforceScalarTypes && scalarTypes.TryGetValue(name, out var previous) && previous != type)
                        throw new InvalidOperationException("Native scalar variable cannot change type.");
                    scalarTypes[name] = type;
                }
            }
            if (FailOn is not null && command.Contains(FailOn)) throw new InvalidOperationException("native read failed");
            if (command.Contains(" = ")) expression = command.Split(" = ", 2)[1];
        }
        public double ReadReal(string variable) => Upper;
        public string ReadString(string variable)
        {
            var slot = Regex.Match(expression, @"\[(\d+)\]");
            var index = slot.Success ? int.Parse(slot.Groups[1].Value) : 1;
            if (expression.StartsWith("!!YUZUHAUIEXISTS") && expression.EndsWith(".string()")) return definedValue;
            if (MissingSlot != 0 && index == MissingSlot) throw new InvalidOperationException("Native array element does not exist.");
            if (expression == "!!Sample.objectType()") return FormObjectType;
            if (expression.EndsWith(".objectType()")) return expression.Contains(".attribute(") ? index == 2 ? "GADGET" : "STRING" : DetailObjectType;
            if (expression.EndsWith(".name()")) return "Form" + index;
            if (expression.EndsWith(".type()")) return "BUTTON";
            if (expression.EndsWith(".set().string()")) return GadgetSet ? "TRUE" : "FALSE";
            if (expression.EndsWith(".shown().string()")) return "TRUE";
            if (expression.EndsWith(".callback")) return "!!SideEffect()";
            if (Regex.IsMatch(expression, @"\[\d+\]$")) return index == 2 ? "accept" : "ordinary";
            return "native metadata";
        }
    }

    private sealed class FakeService : IPmlCommandService
    {
        public RunPmlCommandRequest? Request;
        public Task<HostIdentityResponse> GetHostIdentityAsync(HostIdentityRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<RunPmlCommandResponse> RunPmlCommandAsync(RunPmlCommandRequest request, CancellationToken token)
        { Request = request; return Task.FromResult(new RunPmlCommandResponse { Success = true }); }
    }
}
