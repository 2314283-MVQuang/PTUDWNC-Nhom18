/** Dữ liệu JSON-LD nằm trong thẻ script HTML: escape < để nội dung không đóng thẻ sớm. */
export function serializeJsonLd(value: unknown): string {
  return JSON.stringify(value).replace(/</g, "\\u003c");
}
