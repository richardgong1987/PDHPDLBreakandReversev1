namespace cAlgo.Robots;

// 开口扩大闸门（GapX）的设置。Pure data: no cAlgo.API references.
public class PdhpdlGapXConfigModel {
    // false 时整道闸门不存在：GapX 照常算、照常写进 CSV，但不参与任何判断，
    // 交易逻辑与加这个开关之前完全一致。
    public bool IsEnabled { get; set; }

    // 阈值只取 0 以上。GapX 原始值可正可负（开口收窄为负），但「要求开口继续收窄」
    // 不是这道闸门的用途，所以负阈值在组合根就被夹成 0。
    public double Threshold { get; set; }
}
