export interface PresignedUploadInput { fileName: string; contentType: string; size: number }
export interface PresignedUploadResponse extends PresignedUploadInput {
  fileId: string;
  uploadUrl: string;
  publicUrl: string;
  storageKey: string;
  expiresAt: string;
}
export interface UploadedFile extends PresignedUploadInput {
  id: string;
  storageKey: string;
  url: string;
  bucketName: string;
  uploadedBy: string | null;
  createdAt: string;
}
