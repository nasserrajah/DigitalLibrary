namespace Library.Domain.Constants;

public static class StorageConstants
{
    public const long UserStorageLimitBytes = 500L * 1024 * 1024; // 500 MB
    public const int  MaxCategoryCount      = 20;
    public const long MaxBookFileBytes      = 200L * 1024 * 1024; // 200 MB per book
    public const long MaxCoverFileBytes     = 5L   * 1024 * 1024;  // 5 MB

    public static readonly string[] AllowedBookExtensions =
        { ".pdf", ".epub", ".mobi", ".txt" };

    public static readonly string[] AllowedImageExtensions =
        { ".jpg", ".jpeg", ".png", ".webp" };
}