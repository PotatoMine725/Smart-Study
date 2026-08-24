using S0Pilot;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var cmd = args.Length > 0 ? args[0] : "help";
return cmd switch
{
    "tokcheck" => TokCheck.Run(args),
    "stress" => Stress.Run(args),
    _ => Help(),
};

static int Help()
{
    Console.WriteLine("""
        S0 pilot harness (throwaway -- outside SmartStudyPlanner.slnx)

          tokcheck [--corrupt-vocab] [--no-offset]   EVA-08 output 6 / TOK-02, DAT-05 set
  stress   [--trimmed]                       whitespace-axis characterisation

        Arm C is not runnable: unlocked only by an explicit owner decision (EVA-06).
        """);
    return 0;
}
