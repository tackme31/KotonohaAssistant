using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;
using Moq;

namespace KotonohaAssistant.AI.Tests.Services;

public class SisterSwitchingServiceTests
{
    private readonly Mock<ILogger> _mockLogger;

    public SisterSwitchingServiceTests()
    {
        _mockLogger = new Mock<ILogger>();
    }

    private SisterSwitchingService CreateService()
    {
        return new SisterSwitchingService(_mockLogger.Object);
    }

    #region TrySwitchSister Tests

    [Fact]
    public void TrySwitchSister_WithAkaneName_ShouldSwitchToAkane()
    {
        // TODO: 「茜ちゃん」が含まれる場合、茜に切り替えることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void TrySwitchSister_WithAoiName_ShouldSwitchToAoi()
    {
        // TODO: 「葵ちゃん」が含まれる場合、葵に切り替えることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void TrySwitchSister_WithHiraganaAkaneName_ShouldSwitchToAkane()
    {
        // TODO: 「あかねちゃん」が含まれる場合、茜に切り替えることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void TrySwitchSister_WithHiraganaAoiName_ShouldSwitchToAoi()
    {
        // TODO: 「あおいちゃん」が含まれる場合、葵に切り替えることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void TrySwitchSister_WhenNoNameMatches_ShouldReturnFalse()
    {
        // TODO: 姉妹の名前が含まれない場合、falseを返すことを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void TrySwitchSister_WhenSameSister_ShouldReturnFalse()
    {
        // TODO: 同じ姉妹の場合、falseを返すことを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void TrySwitchSister_WhenBothNamesPresent_ShouldSwitchToFirst()
    {
        // TODO: 両方の名前が含まれる場合、最初に見つかった方に切り替えることを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void TrySwitchSister_WhenSwitched_ShouldReturnTrue()
    {
        // TODO: 姉妹が切り替わった場合、trueを返すことを検証
        throw new NotImplementedException();
    }

    [Fact]
    public void TrySwitchSister_WhenSwitched_ShouldLogInformation()
    {
        // TODO: 姉妹が切り替わった場合、情報ログが記録されることを検証
        throw new NotImplementedException();
    }

    #endregion
}
