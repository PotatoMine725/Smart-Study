using S0Pilot;

Console.OutputEncoding = System.Text.Encoding.UTF8;
// Report in one culture, so a decimal comma never reads as a thousands separator.
System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
var cmd = args.Length > 0 ? args[0] : "help";
return cmd switch
{
    "tokcheck" => TokCheck.Run(args),
    "stress" => Stress.Run(args),
    "accuracy" => Accuracy.Run(args),
    "runtime" => Runtime.Run(args),
    "sanity" => Sanity.Run(args),
    _ => Help(),
};

static int Help()
{
    Console.WriteLine(
        "S0 pilot harness (throwaway -- outside SmartStudyPlanner.slnx)\n\n" +
        "  tokcheck [--corrupt-vocab] [--no-offset]           EVA-08 output 6 / TOK-02, DAT-05 set\n" +
        "  stress   [--trimmed]                               whitespace-axis characterisation\n" +
        "  accuracy [all|baseline|arm_a|arm_b] [--quantized]  EVA-08 outputs 1 and 2\n" +
        "  runtime  <arm_a|arm_b> [--quantized]               EVA-08 outputs 3, 4, 5\n\n" +
        "Arm C is not runnable: unlocked only by an explicit owner decision (EVA-06).");
    return 0;
}
