namespace SmartStudyPlanner.ViewModels
{
    /// <summary>
    /// One slice of the status donut chart.
    /// Key ∈ {"Urgent","Warn","Safe","Done"} — resolved to SeverityXxx brush by DonutChart.
    /// </summary>
    public sealed record StatusSegment(string Key, string Label, int Count);

    /// <summary>
    /// Per-subject time data for the grouped bar chart (Band A right).
    /// Expected and Actual are in minutes.
    /// </summary>
    public sealed record SubjectTimeProgress(string Subject, double Expected, double Actual);

    /// <summary>
    /// Per-subject task-count data for the horizontal bar chart (Band B left).
    /// </summary>
    public sealed record SubjectWorkload(string Subject, int Count);
}
