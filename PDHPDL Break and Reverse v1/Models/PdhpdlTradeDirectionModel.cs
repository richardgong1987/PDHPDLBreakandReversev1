namespace cAlgo.Robots;

// Trade side, kept free of cAlgo.API.TradeType so the planner stays pure and testable.
// The executor maps this to TradeType only at the broker boundary.
public enum PdhpdlTradeDirectionModel {
    Long,
    Short
}
