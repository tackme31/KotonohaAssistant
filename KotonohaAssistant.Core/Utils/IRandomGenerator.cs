using System;

namespace KotonohaAssistant.Core.Utils;

/// <summary>
/// 乱数生成の抽象化インターフェース（テスト可能性のため）
/// </summary>
public interface IRandomGenerator
{
    /// <summary>
    /// 0.0以上1.0未満の乱数を返します
    /// </summary>
    double NextDouble();
}

/// <summary>
/// 本番環境用のランダム実装
/// </summary>
public class SystemRandomGenerator : IRandomGenerator
{
    private readonly Random _random = new();

    public double NextDouble() => _random.NextDouble();
}
