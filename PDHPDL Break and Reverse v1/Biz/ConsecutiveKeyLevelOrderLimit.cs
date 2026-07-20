namespace cAlgo.Robots;

// 连续在同一个关键位（PDH / PDL）下单的次数上限。
// 只统计真正提交成功的订单：连续 N 单都打在同一个关键位后，该关键位被封锁；
// 一旦在另一个关键位下单，连续次数从头开始计算。
// 上限为 0 或负数表示不限制。
public class ConsecutiveKeyLevelOrderLimit {
    private readonly int _maxConsecutiveOrders;
    private string _lastKeyLevel = "";
    private int _consecutiveOrders;

    public ConsecutiveKeyLevelOrderLimit(int maxConsecutiveOrders) {
        _maxConsecutiveOrders = maxConsecutiveOrders;
    }

    public bool IsUnlimited => _maxConsecutiveOrders <= 0;

    public bool HasReachedConsecutiveLimit(string keyLevel) {
        if (!IsCounted(keyLevel))
            return false;

        return keyLevel == _lastKeyLevel && _consecutiveOrders >= _maxConsecutiveOrders;
    }

    public void RecordPlacedOrder(string keyLevel) {
        if (!IsCounted(keyLevel))
            return;

        _consecutiveOrders = keyLevel == _lastKeyLevel ? _consecutiveOrders + 1 : 1;
        _lastKeyLevel = keyLevel;
    }

    private bool IsCounted(string keyLevel) {
        return !IsUnlimited && !string.IsNullOrEmpty(keyLevel);
    }
}
