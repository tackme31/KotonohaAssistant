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

    private ConversationState CreateState(Kotonoha initialSister)
    {
        return new ConversationState
        {
            CurrentSister = initialSister,
            CharacterPromptAkane = "test prompt akane",
            CharacterPromptAoi = "test prompt aoi"
        };
    }

    #region TrySwitchSister Tests

    [Fact]
    public void TrySwitchSister_WithAkaneName_ShouldSwitchToAkane()
    {
        // Arrange
        var service = CreateService();
        var state = CreateState(Kotonoha.Aoi);

        // Act
        var result = service.TrySwitchSister("茜ちゃん、こんにちは", state);

        // Assert
        Assert.True(result);
        Assert.Equal(Kotonoha.Akane, state.CurrentSister);
    }

    [Fact]
    public void TrySwitchSister_WithAoiName_ShouldSwitchToAoi()
    {
        // Arrange
        var service = CreateService();
        var state = CreateState(Kotonoha.Akane);

        // Act
        var result = service.TrySwitchSister("葵ちゃん、こんにちは", state);

        // Assert
        Assert.True(result);
        Assert.Equal(Kotonoha.Aoi, state.CurrentSister);
    }

    [Fact]
    public void TrySwitchSister_WithHiraganaAkaneName_ShouldSwitchToAkane()
    {
        // Arrange
        var service = CreateService();
        var state = CreateState(Kotonoha.Aoi);

        // Act
        var result = service.TrySwitchSister("あかねちゃん、こんにちは", state);

        // Assert
        Assert.True(result);
        Assert.Equal(Kotonoha.Akane, state.CurrentSister);
    }

    [Fact]
    public void TrySwitchSister_WithHiraganaAoiName_ShouldSwitchToAoi()
    {
        // Arrange
        var service = CreateService();
        var state = CreateState(Kotonoha.Akane);

        // Act
        var result = service.TrySwitchSister("あおいちゃん、こんにちは", state);

        // Assert
        Assert.True(result);
        Assert.Equal(Kotonoha.Aoi, state.CurrentSister);
    }

    [Fact]
    public void TrySwitchSister_WhenNoNameMatches_ShouldReturnFalse()
    {
        // Arrange
        var service = CreateService();
        var state = CreateState(Kotonoha.Akane);

        // Act
        var result = service.TrySwitchSister("こんにちは、元気ですか？", state);

        // Assert
        Assert.False(result);
        Assert.Equal(Kotonoha.Akane, state.CurrentSister);
    }

    [Fact]
    public void TrySwitchSister_WhenSameSister_ShouldReturnFalse()
    {
        // Arrange
        var service = CreateService();
        var state = CreateState(Kotonoha.Akane);

        // Act
        var result = service.TrySwitchSister("茜ちゃん、こんにちは", state);

        // Assert
        Assert.False(result);
        Assert.Equal(Kotonoha.Akane, state.CurrentSister);
    }

    [Fact]
    public void TrySwitchSister_WhenBothNamesPresent_ShouldSwitchToFirst()
    {
        // Arrange
        var service = CreateService();
        var state = CreateState(Kotonoha.Akane);

        // Act: 葵ちゃんが先に出現
        var result = service.TrySwitchSister("葵ちゃん、茜ちゃん、こんにちは", state);

        // Assert
        Assert.True(result);
        Assert.Equal(Kotonoha.Aoi, state.CurrentSister);
    }

    [Fact]
    public void TrySwitchSister_WhenSwitched_ShouldReturnTrue()
    {
        // Arrange
        var service = CreateService();
        var state = CreateState(Kotonoha.Akane);

        // Act
        var result = service.TrySwitchSister("葵ちゃん、こんにちは", state);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TrySwitchSister_WhenSwitched_ShouldLogInformation()
    {
        // Arrange
        var service = CreateService();
        var state = CreateState(Kotonoha.Akane);

        // Act
        service.TrySwitchSister("葵ちゃん、こんにちは", state);

        // Assert
        _mockLogger.Verify(
            x => x.LogInformation(It.Is<string>(s => s.Contains("[SisterSwitch]") && s.Contains("Akane") && s.Contains("Aoi"))),
            Times.Once);
    }

    #endregion
}
