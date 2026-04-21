namespace DocumentFlowApp.Core.Enums
{
    // Статусы документа в процессе жизненного цикла
    public enum DocumentStatus
    {
        Draft = 1,      // Черновик
        OnApproval = 2, // На согласовании
        Approved = 3,   // Утвержден
        InWork = 4,     // В работе (исполнение)
        Completed = 5,  // Завершен
        Archived = 6    // В архиве
    }
}
