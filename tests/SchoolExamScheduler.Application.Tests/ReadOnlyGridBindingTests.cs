using System.Xml.Linq;
using SchoolExamScheduler.Domain;
using Xunit;

public sealed class ReadOnlyGridBindingTests
{
    [Theory]
    [InlineData("AvailabilityView.xaml", "IsAvailable")]
    [InlineData("ReviewView.xaml", "IsLocked")]
    [InlineData("SubjectsView.xaml", "IsActive")]
    [InlineData("VenuesView.xaml", "IsActive")]
    [InlineData("TeachersView.xaml", "IsActive")]
    [InlineData("TeachersView.xaml", "CanInvigilate")]
    public void DisplayOnlyCheckboxColumnsNeverRequestSourceUpdates(string view, string property)
    {
        // IsReadOnly on a grid does not override ToggleButton.IsChecked's default
        // TwoWay binding. Inspect the actual shipped XAML, not a duplicated fixture.
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "DesktopViews", view));
        var column = Assert.Single(document.Descendants(presentation + "DataGridCheckBoxColumn"),
            c => ((string?)c.Attribute("Binding"))?.StartsWith("{Binding " + property, StringComparison.Ordinal) == true);
        var grid = column.Ancestors(presentation + "DataGrid").Single();
        Assert.Equal("True", (string?)grid.Attribute("IsReadOnly"));
        Assert.Contains("Mode=OneWay", (string?)column.Attribute("Binding") ?? "");
    }

    [Fact]
    public void AvailabilityPolicyCanOnlyBeChangedThroughValidatedDomainOperations()
    {
        var property = typeof(TeacherAvailability).GetProperty(nameof(TeacherAvailability.IsAvailable))!;
        Assert.Null(property.GetSetMethod(nonPublic: false));
    }
}
