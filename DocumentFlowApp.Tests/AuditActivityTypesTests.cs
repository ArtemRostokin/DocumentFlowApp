using DocumentFlowApp.Core.Audit;

namespace DocumentFlowApp.Tests;

public class AuditActivityTypesTests
{
    [Fact]
    public void All_Contains_Document_And_System_Audit_Types()
    {
        Assert.Contains(AuditActivityTypes.DocumentCreated, AuditActivityTypes.All);
        Assert.Contains(AuditActivityTypes.StatusChanged, AuditActivityTypes.All);
        Assert.Contains(AuditActivityTypes.UserLogin, AuditActivityTypes.All);
        Assert.Contains(AuditActivityTypes.UserLogout, AuditActivityTypes.All);
        Assert.Contains(AuditActivityTypes.UserCreated, AuditActivityTypes.All);
        Assert.Contains(AuditActivityTypes.UserUpdated, AuditActivityTypes.All);
        Assert.Contains(AuditActivityTypes.UserPasswordReset, AuditActivityTypes.All);
        Assert.Contains(AuditActivityTypes.NomenclatureCaseCreated, AuditActivityTypes.All);
        Assert.Contains(AuditActivityTypes.NomenclatureRuleCreated, AuditActivityTypes.All);
    }

    [Theory]
    [InlineData(AuditActivityTypes.DocumentCreated, "Создание документа")]
    [InlineData(AuditActivityTypes.ExecutionFileUploaded, "Загрузка итогового файла")]
    [InlineData(AuditActivityTypes.UserLogin, "Вход в систему")]
    [InlineData(AuditActivityTypes.UserCreated, "Создание пользователя")]
    [InlineData(AuditActivityTypes.UserPasswordReset, "Сброс пароля пользователя")]
    public void GetDisplayName_Returns_Expected_Label(string activityType, string expectedLabel)
    {
        var label = AuditActivityTypes.GetDisplayName(activityType);

        Assert.Equal(expectedLabel, label);
    }

    [Fact]
    public void GetDisplayName_Falls_Back_For_Unknown_Type()
    {
        var label = AuditActivityTypes.GetDisplayName("custom-type");

        Assert.Equal("Прочее действие", label);
    }
}
