namespace Orts.ActivityRunner.Processes
{
    public enum ProcessType
    {
        Render,
        Updater,
        Loader,
        Sound,
        System,
        WebServer,
    }

    public enum SlidingMetric
    {
        ProcessorTime,
        FrameRate,
        FrameTime,
    }

    public enum DiagnosticInfo
    {
        System,
        Clr,
        ProcessMetric,
        GpuMetric,
    }
}
