namespace Landsong.InputSystem
{
    /// <summary>
    /// 项目级输入交互阈值。业务组件不得重复声明同义判定参数。
    /// </summary>
    public static class InteractionConstants
    {
        public const float DoubleClickIntervalSeconds = 0.3f;
        public const float LongPressDurationSeconds = 0.45f;
        public const float ClickMovementTolerancePixels = 8f;
    }
}
