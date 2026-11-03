"""Tạo báo cáo Word Lab 8 từ mẫu Lab 4; chỉ dùng thư viện chuẩn Python."""
from __future__ import annotations

import argparse
from copy import deepcopy
from datetime import datetime, timezone
import json
from pathlib import Path
import tempfile
import xml.etree.ElementTree as ET
import zipfile

W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
NS = {"w": W, "r": R}
ET.register_namespace("w", W)
ET.register_namespace("r", R)


def tag(name: str) -> str:
    return f"{{{W}}}{name}"


def paragraph(text: str, *, bold: bool = False, size: int = 28, props=None):
    p = ET.Element(tag("p"))
    if props is not None:
        p.append(deepcopy(props))
    else:
        pr = ET.SubElement(p, tag("pPr"))
        ET.SubElement(pr, tag("spacing"), {tag("after"): "100"})
    for index, part in enumerate(text.split("\t")):
        run = ET.SubElement(p, tag("r"))
        rp = ET.SubElement(run, tag("rPr"))
        ET.SubElement(rp, tag("rFonts"), {tag("ascii"): "Times New Roman", tag("hAnsi"): "Times New Roman", tag("cs"): "Times New Roman"})
        ET.SubElement(rp, tag("sz"), {tag("val"): str(size)})
        if bold:
            ET.SubElement(rp, tag("b"))
        if index:
            ET.SubElement(run, tag("tab"))
        ET.SubElement(run, tag("t"), {"{http://www.w3.org/XML/1998/namespace}space": "preserve"}).text = part
    return p


def contents(result):
    return [
        ("Điều chỉnh giao diện frontend và trang chi tiết công thức", [
            "Thu gọn vùng tiêu đề, sửa lớp hiển thị dải thông số để thời gian và khẩu phần không bị che. Giữ màu cam của giao diện hiện có, điều chỉnh khoảng cách giữa nội dung và chân trang.",
            "Bố trí các bước thực hiện và nguyên liệu thành hai cột trên màn hình lớn; chuyển thành một cột trên điện thoại. Ẩn khung dinh dưỡng khi chưa có dữ liệu để tránh hiển thị một bảng trống.",
            "Kiểm tra trang công khai sau khi lưu trữ hoặc xóa công thức; không giữ nội dung đã gỡ trong cache Next.js. Có ảnh minh chứng giao diện desktop và mobile."
        ]),
        ("Hoàn thiện API upload ảnh theo SRS", [
            "Đồng bộ POST /api/v1/recipes/{id}/images với multipart/form-data, gồm tệp file và mô tả altText tùy chọn. Kiểm tra tác giả sở hữu hoặc Admin trước khi lưu ảnh.",
            "Giới hạn 5 MB; chấp nhận JPEG, PNG, WebP và AVIF. Kiểm tra đồng thời phần mở rộng, MIME type và chữ ký đầu tệp để từ chối ảnh giả hoặc tệp đổi đuôi.",
            "Lưu ảnh vào MinIO với khóa recipes/{recipeId}/{uuid}.{ext}; lưu metadata ảnh qua repository và Unit of Work. Bổ sung xử lý bù trừ khi lưu metadata thất bại và phản hồi Problem Details khi kho ảnh gián đoạn."
        ]),
        ("Quản lý ảnh trong trang thêm mới và chỉnh sửa công thức", [
            "Bổ sung chọn ảnh ở bước xem lại khi tạo công thức. Sau khi tạo bản nháp thành công, tải ảnh lên và chuyển sang trang chỉnh sửa. Nếu tải ảnh thất bại, giữ ID bản nháp đã tạo để thử lại mà không tạo trùng công thức.",
            "Hoàn thiện trình quản lý ảnh: hiển thị ảnh từ MinIO, nhập mô tả, đặt ảnh đại diện và xóa ảnh. Đồng bộ thao tác đặt ảnh đại diện với endpoint PATCH /images/{imageId}/primary.",
            "Ảnh đầu tiên tự thành ảnh đại diện. Khi xóa ảnh đại diện, chọn ảnh còn lại theo thứ tự trong cùng transaction; job Hangfire dọn đối tượng ảnh trên MinIO, có ba lần thử lại khi lỗi."
        ]),
        ("Hoàn thiện vòng đời công thức và trang quản lý", [
            "Rà soát và sử dụng các API tạo, sửa, Publish, Archive, khôi phục trạng thái và xóa mềm đã có. Hoàn thiện giao diện thông báo tải/lỗi, xác nhận xóa và kiểm tra quyền trước khi hiển thị biểu mẫu chỉnh sửa.",
            "Bổ sung tham số mine và status để lọc trên server trước khi phân trang. Tác giả xem công thức thuộc sở hữu; Admin có thể chọn toàn bộ hoặc công thức thuộc tài khoản hiện tại.",
            "Giữ xử lý If-Match/RowVersion và lỗi 409 khi có cập nhật bằng phiên bản cũ. Deleted được biểu diễn bằng IsDeleted/DeletedAt theo quyết định SRS của nhóm; dữ liệu không bị xóa vật lý ngay."
        ]),
        ("Xây dựng API và trang danh sách người dùng", [
            "Thêm GET /api/v1/users chỉ dành cho Admin, kiểm tra quyền ở cả endpoint và handler. Truy vấn người dùng/role theo trang và trả DTO gồm thông tin hiển thị; không trả PasswordHash, SecurityStamp hoặc refresh token.",
            "Cài đặt tìm kiếm tên/email, lọc Admin/Author và phân trang với pageSize tối đa 50. Bổ sung kiểu dữ liệu TypeScript và trang /dashboard/users, trạng thái tải, lỗi và kết quả rỗng.",
            "Bảo vệ đường dẫn bằng middleware, chỉ hiển thị mục Người dùng trong menu Admin và không cache phản hồi HTTP chứa danh sách tài khoản. Kiểm tra Author bị từ chối tại giao diện và API."
        ]),
        ("Bổ sung nhật ký hệ thống", [
            "Bổ sung log HTTP chứa method, path, status, elapsed time, UserId, CorrelationId và TraceId. Cảnh báo khi thời gian xử lý vượt 500 ms hoặc có lỗi; giữ logging cho Command/Query trong MediatR.",
            "Cấu hình Console dạng JSON, File rolling theo ngày và giữ 30 tệp; Seq dùng trong Development. Kiểm tra độ dài/ký tự CorrelationId để tránh ghi dữ liệu header tùy ý vào log.",
            "Không ghi body đăng nhập, mật khẩu, JWT, refresh token hoặc token xác nhận/đặt lại mật khẩu vào nhật ký. Có kiểm thử đọc tệp JSONL thật và kiểm tra sự vắng mặt của các giá trị bí mật."
        ]),
        ("Kiểm thử tích hợp và kiểm tra giao diện Production", [
            f"Chạy bộ tích hợp PostgreSQL, Redis và MinIO tạm: {result['integration']['passed']}/{result['integration']['total']} ca đạt. Bao phủ Auth + Recipe, upload/đổi ảnh đại diện/xóa đối tượng, quyền người dùng, lọc trạng thái, phân trang và logging.",
            f"Chạy hồi quy mặc định: {result['regression']['passed']} đạt, {result['regression']['skipped']} bỏ qua theo điều kiện môi trường, {result['regression']['failed']} lỗi. Các ca bỏ qua không được tính là đã đạt trong lần chạy đó.",
            "Chạy Edge headless trên bản frontend standalone Production và backend thật của phiên thử. Kiểm tra ảnh giả, ảnh hợp lệ, vòng đời công thức, trang công khai, danh sách Admin và bố cục điện thoại. TypeScript, ESLint và build Production đều đạt."
        ]),
        ("Đối chiếu SRS, chuẩn bị demo và cập nhật tài liệu", [
            "Đối chiếu phần ảnh tại SRS trang 33–34, logging tại trang 39 và các quyết định đã thống nhất về soft delete, Instructions tùy chọn, RowVersion và cache.",
            "Cập nhật báo cáo Markdown, bảng phân công đúng phần đã thực hiện, kịch bản demo và ảnh minh chứng. Phiên demo chuẩn bị tài khoản Admin/Author và dữ liệu trong container riêng, không dùng dữ liệu nhóm hoặc LMS.",
            "Giới hạn: chưa nhận hoàn thành toàn bộ 34 FR; thumbnail, quản trị khóa/sửa vai trò tài khoản, Google OAuth thực tế và triển khai server không thuộc phạm vi bổ sung này. Liên kết nhánh dùng để tham chiếu; chưa xác nhận các thay đổi đã được push."
        ]),
    ]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--template", type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = root / "spec/Lab8_2312804_ChungThienY.docx"
    result = json.loads((root / "spec/minh_chung_lab8_2026_11_03/ket_qua_kiem_thu.json").read_text(encoding="utf-8"))
    if not result.get("completed"):
        raise SystemExit("Chỉ tạo báo cáo hoàn thành sau khi kết quả kiểm chứng đã được ghi nhận.")
    if args.template.resolve() == output.resolve():
        raise SystemExit("Không được ghi đè tệp mẫu.")
    with zipfile.ZipFile(args.template) as source:
        old = ET.fromstring(source.read("word/document.xml"))
        old_body = old.find("w:body", NS)
        template_table = old_body.find("w:tbl", NS)
        table = deepcopy(template_table)
        original_rows = table.findall("w:tr", NS)
        row_template = deepcopy(original_rows[1])
        for row in original_rows[1:]:
            table.remove(row)
        header = original_rows[0]
        header_props = header.find("w:trPr", NS)
        if header_props is None:
            header_props = ET.SubElement(header, tag("trPr"))
        ET.SubElement(header_props, tag("tblHeader"))
        for number, (title, details) in enumerate(contents(result), 1):
            row = deepcopy(row_template)
            cells = row.findall("w:tc", NS)
            for index, cell in enumerate(cells):
                if index == 2:
                    for text in cell.findall(".//w:t", NS):
                        text.text = "Nhánh Chung Thiện Ý – nhóm 18" if text is cell.find(".//w:t", NS) else ""
                    continue
                for child in list(cell):
                    if child.tag != tag("tcPr"):
                        cell.remove(child)
                if index == 0:
                    cell.append(paragraph(str(number)))
                elif index == 1:
                    cell.append(paragraph(title, bold=True))
                    cell.append(paragraph("Đã hoàn thành:", bold=True))
                    for detail in details:
                        cell.append(paragraph(detail))
                    cell.append(paragraph("Chưa hoàn thành trong phạm vi mục này: Không có."))
                else:
                    cell.append(paragraph("100%"))
            table.append(row)
        document = ET.Element(tag("document"))
        body = ET.SubElement(document, tag("body"))
        old_paragraphs = old_body.findall("w:p", NS)
        body.append(paragraph("BÁO CÁO LAB – MÔN PHÁT TRIỂN ỨNG DỤNG WEB NÂNG CAO", bold=True, size=36, props=old_paragraphs[0].find("w:pPr", NS)))
        body.append(paragraph("Lab: 08\tTừ ngày: 27/10/2026\tđến ngày: 03/11/2026", props=old_paragraphs[2].find("w:pPr", NS)))
        body.append(paragraph("MSSV: 2312804\tHọ và tên: Chung Thiện Ý\tNhóm: 18", bold=True))
        body.append(paragraph("Công việc:", bold=True))
        body.append(table)
        body.append(paragraph("Ghi chú về phạm vi và kiểm chứng", bold=True))
        body.append(paragraph("Tiến độ phản ánh các mục Lab 8 đã được xác nhận mở rộng phạm vi; các module được kế thừa từ thành viên khác được ghi nhận là tích hợp/kiểm thử, không nhận là toàn bộ phần lập trình mới."))
        body.append(paragraph("Ảnh và kết quả kiểm thử: spec/minh_chung_lab8_2026_11_03. Báo cáo giải thích chi tiết: spec/bao_cao_ChungThienY_tuan_8.md. Liên kết nhánh không thay thế việc kiểm tra trạng thái commit/push."))
        body.append(deepcopy(old_body.find("w:sectPr", NS)))
        # Chuẩn hóa chữ trong bảng, giữ bố cục A4 ngang/bốn cột của mẫu.
        for p in table.findall(".//w:p", NS):
            p.attrib.clear()
        for run in table.findall(".//w:r", NS):
            rp = run.find("w:rPr", NS)
            if rp is None:
                rp = ET.Element(tag("rPr")); run.insert(0, rp)
            fonts = rp.find("w:rFonts", NS)
            if fonts is None:
                fonts = ET.SubElement(rp, tag("rFonts"))
            fonts.set(tag("ascii"), "Times New Roman"); fonts.set(tag("hAnsi"), "Times New Roman")
            size = rp.find("w:sz", NS)
            if size is None:
                size = ET.SubElement(rp, tag("sz"))
            size.set(tag("val"), "28")
        xml = ET.tostring(document, encoding="utf-8", xml_declaration=True)
        core = ET.fromstring(source.read("docProps/core.xml"))
        cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
        dc = "http://purl.org/dc/elements/1.1/"
        dt = "http://purl.org/dc/terms/"
        for prefix, namespace in (("cp", cp), ("dc", dc), ("dcterms", dt), ("xsi", "http://www.w3.org/2001/XMLSchema-instance")):
            ET.register_namespace(prefix, namespace)
        metadata = {
            f"{{{dc}}}title": "Báo cáo Lab 8 – Chung Thiện Ý – 2312804",
            f"{{{dc}}}subject": "Phát triển ứng dụng web nâng cao – Nhóm 18",
            f"{{{dc}}}creator": "Chung Thiện Ý",
            f"{{{cp}}}lastModifiedBy": "Chung Thiện Ý",
            f"{{{cp}}}revision": "1",
            f"{{{dt}}}created": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
            f"{{{dt}}}modified": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        }
        for key, value in metadata.items():
            element = core.find(key)
            if element is None:
                element = ET.SubElement(core, key)
            element.text = value
        core_xml = ET.tostring(core, encoding="utf-8", xml_declaration=True)
        with tempfile.NamedTemporaryFile(dir=output.parent, suffix=".docx", delete=False) as pending:
            temporary = Path(pending.name)
        try:
            with zipfile.ZipFile(temporary, "w", zipfile.ZIP_DEFLATED) as target:
                for item in source.infolist():
                    payload = xml if item.filename == "word/document.xml" else core_xml if item.filename == "docProps/core.xml" else source.read(item.filename)
                    target.writestr(item, payload)
            with zipfile.ZipFile(temporary) as check:
                assert check.testzip() is None
                verified = ET.fromstring(check.read("word/document.xml"))
                rows = verified.findall(".//w:tbl/w:tr", NS)
                assert len(rows) == 9 and all(len(row.findall("w:tc", NS)) == 4 for row in rows)
                assert verified.find(".//w:tblHeader", NS) is not None
            temporary.replace(output)
        finally:
            temporary.unlink(missing_ok=True)
    print(output.name)


if __name__ == "__main__":
    main()
