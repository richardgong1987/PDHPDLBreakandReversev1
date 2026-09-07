namespace cAlgo.Robots;

// 一个可开仓的关键价位：Name 会写进 signalModel.KeyLevel 和交易 CSV。
// Price 为 0 表示这一档没有设置（手工输入的 Pdh1/Pdl1 默认就是 0），不参与判断。
public class PdhpdlKeyLevelModel {
    public PdhpdlKeyLevelModel(string name, double price) {
        Name = name;
        Price = price;
    }

    public string Name { get; }

    public double Price { get; }

    public bool IsConfigured => Price > 0;
}
