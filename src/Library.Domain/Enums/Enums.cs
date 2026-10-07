namespace Library.Domain.Enums;

public enum BookFormat { Pdf = 1, Epub = 2, Mobi = 3, Txt = 4, Other = 99 }
public enum AuthProvider { Local = 1, Google = 2, Facebook = 3 }
public enum LibraryBookStatus { Unread = 1, Reading = 2, Completed = 3, Paused = 4 }
public enum NotificationType { General = 1, NewBook = 2, SystemAlert = 3, BookUpdate = 4 }