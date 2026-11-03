namespace CulinaryBlog.Application.Common.Exceptions;

public sealed class FileStorageUnavailableException(Exception inner)
    : Exception("Kho ảnh đang tạm thời gián đoạn. Vui lòng thử lại sau.", inner);
