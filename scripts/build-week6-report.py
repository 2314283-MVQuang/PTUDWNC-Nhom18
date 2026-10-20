"""Tạo Lab 6 bằng bố cục của Lab 5, không sửa file mẫu. Cần lxml."""
from argparse import ArgumentParser
from copy import deepcopy
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
from lxml import etree

ROOT = Path(__file__).resolve().parents[1]
W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
NS = {"w": W}

# Nội dung cá nhân: tách rõ phần mới, phần tái sử dụng và giới hạn kiểm chứng.
ROWS = [
    [
        "Xây dựng bộ lọc kết quả tìm kiếm công thức (FR-SRCH-002)",
        [
            "Bổ sung bộ lọc cho chức năng tìm kiếm công thức, cho phép người dùng tìm theo danh mục, độ khó, thời gian chuẩn bị và thời gian nấu, đồng thời kết hợp các điều kiện này với từ khóa.",
            "Bổ sung các tham số categoryId, difficulty, minPrepTime, maxPrepTime, minCookTime và maxCookTime vào API tìm kiếm, sau đó chuyển các giá trị qua SearchRecipesQuery đến repository để truy vấn dữ liệu. Ví dụ, khi người dùng chọn món dễ và thời gian nấu tối đa 30 phút, kết quả chỉ gồm những công thức thỏa mãn cả hai điều kiện.",
            "Các điều kiện được áp dụng ngay trong câu truy vấn, trước khi lấy tối đa 50 kết quả. Cách làm này tránh bỏ sót công thức phù hợp nếu chỉ lọc trên danh sách đã tải về. Kết quả chỉ hiển thị công thức đã xuất bản và chưa bị xóa mềm; thời gian ở đúng mức tối thiểu hoặc tối đa vẫn được chấp nhận.",
            "Bổ sung kiểm tra dữ liệu đầu vào, như thời gian âm, khoảng thời gian bị nhập ngược hoặc độ khó không hợp lệ. Những trường hợp này trả lỗi 422 theo dạng ProblemDetails; tham số sai kiểu dữ liệu trả lỗi 400. Tiếp tục sử dụng phần tìm kiếm tiếng Việt không dấu và bộ nhớ đệm đã có của nhóm."
        ],
        "API hiện vẫn trả tối đa 50 kết quả và sắp xếp theo mức độ liên quan. Phần lựa chọn cách sắp xếp và phân trang lần lượt do Thịnh và Tiến phụ trách; khi tích hợp, nhóm cần kiểm tra lại các phần này cùng với bộ lọc."
    ],
    [
        "Hoàn thiện các tiện ích gọi API và xử lý lỗi",
        [
            "Sử dụng các hàm gọi API dùng chung của dự án và bổ sung cách nhận biết lỗi kết nối bằng ApiNetworkError. Nhờ đó, giao diện có thể thông báo phù hợp khi không kết nối được với máy chủ, thay vì xử lý mọi trường hợp như cùng một lỗi.",
            "Điều chỉnh phần đọc lỗi trả về từ API để kiểm tra cấu trúc dữ liệu trước khi sử dụng. Nếu máy chủ trả về nội dung không đúng định dạng, giao diện vẫn hiển thị thông báo lỗi và cho phép người dùng thử lại. Nút Thử lại được cài đặt để tải lại dữ liệu ngay trên trang hiện tại.",
            "Xây dựng các hàm đọc, kiểm tra và đưa bộ lọc lên URL trong tệp search-filters.ts. Các hàm này giữ đúng từ khóa tiếng Việt, phân biệt ô bỏ trống với giá trị 0 và ngăn gửi yêu cầu tìm kiếm khi khoảng thời gian không hợp lệ.",
            "Bổ sung kiểu SearchRecipe theo đúng dữ liệu API tìm kiếm trả về. Việc tập trung các xử lý này vào tiện ích chung giúp giảm lặp mã và dễ sửa hơn khi cần thay đổi."
        ],
        "Khi API tìm kiếm được bổ sung phân trang, phần kiểu dữ liệu và cách đọc kết quả cần được cập nhật theo phản hồi mới."
    ],
    [
        "Cài đặt và sử dụng thư viện hỗ trợ giao diện",
        [
            "Lựa chọn Radix UI để đáp ứng yêu cầu sử dụng thư viện hỗ trợ thiết kế giao diện. Cài đặt gói @radix-ui/react-dialog phiên bản 1.3.1 và sử dụng để xây dựng hộp thoại bộ lọc trên màn hình điện thoại.",
            "Tạo component Dialog.tsx, kết hợp với Tailwind CSS và các nút, biểu tượng sẵn có của dự án. Nhờ vậy, bộ lọc mới có màu sắc và cách trình bày thống nhất với giao diện blog.",
            "Radix hỗ trợ việc di chuyển bằng bàn phím trong hộp thoại, đóng bằng phím Escape và đưa vị trí chọn trở về nút mở sau khi đóng. Cách này giúp giảm phần xử lý tương tác phải tự viết, nhưng ứng dụng cũng cần cài thêm thư viện và tải thêm mã phía trình duyệt."
        ],
        "Phần hộp thoại bộ lọc đã hoàn thành. Một số thư viện có sẵn trong dự án vẫn còn cảnh báo phụ thuộc; việc rà soát và nâng phiên bản cần được nhóm thống nhất riêng."
    ],
    [
        "Cài đặt store quản lý trạng thái bộ lọc",
        [
            "Xây dựng search-filter-store.tsx bằng React Context và useReducer để lưu các giá trị người dùng đang nhập. Store có các thao tác thay đổi từng trường, xóa bộ lọc và đồng bộ lại với điều kiện trên URL.",
            "Biểu mẫu trên máy tính và hộp thoại trên điện thoại sử dụng chung trạng thái. Khi người dùng đóng rồi mở lại hộp thoại, các lựa chọn đang nhập vẫn được giữ lại. Khi xóa bộ lọc, từ khóa tìm kiếm được giữ để người dùng không phải nhập lại.",
            "Tách phần đang nhập trong store với phần đã áp dụng trên URL. Sau khi nhấn Áp dụng, người dùng có thể sao chép liên kết, tải lại trang hoặc dùng nút quay lại, tiến tới của trình duyệt mà vẫn khôi phục được các điều kiện tìm kiếm.",
            "Lựa chọn Context và useReducer vì React đã hỗ trợ sẵn và cách tổ chức này phù hợp với quy mô của bộ lọc. Phần đăng nhập tiếp tục sử dụng SessionProvider của dự án."
        ],
        "Store hiện phục vụ màn hình tìm kiếm và chưa lưu dữ liệu nháp vào bộ nhớ trình duyệt. Khi tải lại trang, biểu mẫu được khôi phục từ các điều kiện đã áp dụng trên URL."
    ],
    [
        "Thiết kế giao diện trang tìm kiếm phù hợp với máy tính và điện thoại",
        [
            "Hoàn thiện giao diện trang tìm kiếm với phần tiêu đề, khu vực bộ lọc và danh sách công thức. Trên máy tính, bộ lọc nằm bên trái và kết quả nằm bên phải. Trên điện thoại, người dùng mở bộ lọc bằng một nút để dành thêm không gian cho nội dung.",
            "Bổ sung các trạng thái hiển thị khi chưa nhập từ khóa, đang tải, không tìm thấy công thức, nhập sai bộ lọc hoặc gặp lỗi từ máy chủ. Các ô nhập có nhãn rõ ràng; thời gian chuẩn bị và thời gian nấu được chia thành từng nhóm để dễ thao tác.",
            "Sau khi hoàn thiện bố cục, kết nối trang với API /recipes/search. Giao diện hiển thị số công thức thực tế nhận được và nhắc người dùng thu hẹp điều kiện khi danh sách đạt giới hạn 50 kết quả.",
            "Sử dụng lại màu sắc, kiểu chữ và các thành phần giao diện đã có của nhóm. Cách làm này giúp trang tìm kiếm thống nhất với các trang còn lại của blog."
        ],
        "Giao diện đã được kiểm tra trên kích thước màn hình máy tính và điện thoại bằng dữ liệu giả lập. Việc kiểm tra toàn bộ blog với dữ liệu thực của nhóm chưa nằm trong kết quả kiểm thử này."
    ],
    [
        "Xây dựng và sử dụng các component dùng chung",
        [
            "Bổ sung các component Dialog, LoadingState, ErrorState, EmptyState và RetryButton để dùng cho hộp thoại, trạng thái chờ, thông báo lỗi, danh sách rỗng và thao tác thử lại.",
            "Phần bộ lọc được tách vào SearchFilters, còn phần hiển thị công thức được tách vào SearchResults. Nhờ đó, trang tìm kiếm dễ đọc hơn và mỗi component có một nhiệm vụ cụ thể.",
            "Sử dụng lại Header, Footer, Button, Card, DifficultyBadge và Pagination của nhóm. Với Pagination, bổ sung nhãn trang và aria-current để công cụ hỗ trợ đọc màn hình nhận biết trang đang được chọn.",
            "Các component thông báo nhận nội dung qua props nên có thể dùng lại ở màn hình khác. Bổ sung role=status cho trạng thái tải và role=alert cho thông báo lỗi để hỗ trợ người dùng sử dụng công cụ đọc màn hình."
        ],
        "Các component mới đã được sử dụng ở trang tìm kiếm. Việc áp dụng rộng hơn ở các màn hình khác cần được phối hợp với thành viên phụ trách; Pagination sẽ được nối vào Search khi API có dữ liệu phân trang."
    ],
    [
        "Hoàn thiện bố cục chung cho trang blog",
        [
            "Sử dụng bố cục chung trong app/layout.tsx để trang tìm kiếm có cùng phần đầu trang, nội dung chính và chân trang với toàn bộ blog. Nhờ kế thừa layout, mỗi trang không cần viết lại Header và Footer.",
            "Bổ sung liên kết Đến nội dung chính để người dùng bàn phím có thể chuyển nhanh qua phần điều hướng. Khu vực nội dung chính được đặt điểm nhận focus tương ứng.",
            "Điều chỉnh bố cục tìm kiếm để cột kết quả co giãn theo màn hình và hạn chế tràn ngang. Đồng thời, bổ sung loading.tsx và error.tsx cho trang Search để trạng thái chờ hoặc lỗi vẫn hiển thị trong bố cục chung của ứng dụng.",
            "Phần này được thực hiện trên nền layout sẵn có của nhóm, giúp giao diện nhất quán và thuận tiện hơn khi bổ sung nội dung cho trang tìm kiếm."
        ],
        "Phần bố cục phục vụ trang tìm kiếm đã hoàn thành. Các trang nghiệp vụ khác tiếp tục sử dụng layout và cách tổ chức hiện có của nhóm."
    ],
    [
        "Kiểm thử chức năng và tổng hợp kết quả",
        [
            "Bổ sung kiểm thử cho từng bộ lọc và các trường hợp kết hợp nhiều điều kiện. Nội dung kiểm tra gồm khoảng thời gian, dữ liệu đầu vào không hợp lệ, tìm tiếng Việt không dấu và việc loại các công thức nháp, đã lưu trữ hoặc đã xóa mềm khỏi kết quả.",
            "Ở lần kiểm thử ghi nhận ngày 14/10/2026, bộ test backend có 119 trường hợp đạt và 3 trường hợp cần môi trường PostgreSQL/Redis nên được chạy riêng. Khi chạy bộ HTTP Recipe trên PostgreSQL 16 và Redis 7 tạm, cả 30 trường hợp đều đạt, gồm 14 trường hợp cũ và 16 trường hợp Search mới. Ba trường hợp tích hợp nêu trên nằm trong bộ kiểm thử này.",
            "Phía frontend có 13/13 trường hợp đạt, gồm 5 trường hợp đăng nhập có sẵn và 8 trường hợp mới về bộ lọc, store, gọi API và xử lý lỗi. Kiểm tra TypeScript, ESLint và build Production đều đạt. Trong quá trình build vẫn có thông báo không kết nối được backend ở một số trang sẵn có, nhưng build hoàn tất.",
            "Kiểm tra giao diện bằng Edge tự động với API giả lập: áp dụng và xóa bộ lọc, quay lại hoặc tiến tới, mở hộp thoại trên điện thoại, dùng phím Escape, xử lý danh sách rỗng, lỗi và thử lại. Các thao tác được kiểm tra đều đạt; không ghi nhận lỗi JavaScript hoặc tràn ngang trên kích thước mobile đã thử.",
            "Lưu ảnh minh chứng trong spec/minh_chung_tuan_6 và bổ sung nội dung giải thích vào báo cáo tổng hợp tuần 1–6. Việc kiểm thử database sử dụng môi trường tạm riêng để không ảnh hưởng đến dữ liệu đang dùng của nhóm."
        ],
        "Chưa kiểm tra hiệu năng với lượng dữ liệu lớn, độ bao phủ kiểm thử, đăng nhập Google thực tế hoặc bộ lọc kết hợp với phần sắp xếp và phân trang chưa tích hợp. Kết quả trên phản ánh các chức năng và môi trường đã kiểm thử."
    ]
]


def paragraph(text, *, bold=False, color=None, center=False, keep=False):
    p = etree.Element(f"{{{W}}}p")
    pp = etree.SubElement(p, f"{{{W}}}pPr")
    if keep:
        etree.SubElement(pp, f"{{{W}}}keepNext")
    etree.SubElement(pp, f"{{{W}}}spacing", {f"{{{W}}}after": "100", f"{{{W}}}line": "260", f"{{{W}}}lineRule": "auto"})
    if center:
        etree.SubElement(pp, f"{{{W}}}jc", {f"{{{W}}}val": "center"})
    run = etree.SubElement(p, f"{{{W}}}r")
    rp = etree.SubElement(run, f"{{{W}}}rPr")
    etree.SubElement(rp, f"{{{W}}}rFonts", {f"{{{W}}}ascii": "Times New Roman", f"{{{W}}}hAnsi": "Times New Roman"})
    etree.SubElement(rp, f"{{{W}}}sz", {f"{{{W}}}val": "28"})
    etree.SubElement(rp, f"{{{W}}}lang", {f"{{{W}}}val": "vi-VN"})
    if bold:
        etree.SubElement(rp, f"{{{W}}}b")
    if color:
        etree.SubElement(rp, f"{{{W}}}color", {f"{{{W}}}val": color})
    etree.SubElement(run, f"{{{W}}}t").text = text
    return p


def replace_cell(cell, paragraphs):
    for child in list(cell):
        if child.tag != f"{{{W}}}tcPr":
            cell.remove(child)
    cell.extend(paragraphs)


def main():
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--template", type=Path)
    parser.add_argument("--output", type=Path, default=ROOT / "spec/Lab6_2312804_ChungThienY.docx")
    args = parser.parse_args()
    template = args.template or next(ROOT.parents[1].glob("*/Lab5_2312804_ChungThienY.docx"))
    if template.resolve() == args.output.resolve():
        raise ValueError("Không ghi đè file mẫu")
    with ZipFile(template) as archive:
        entries = {entry.filename: archive.read(entry.filename) for entry in archive.infolist()}
    doc = etree.fromstring(entries["word/document.xml"])
    body = doc.find("w:body", NS)
    for text in body.xpath("./w:p//w:t", namespaces=NS):
        text.text = (text.text or "").replace("Lab: 05", "Lab: 06")
        if text.text == "Từ ngày: 06/10/2026":
            text.text = "Từ ngày: 13/10/2026"
        elif text.text == "đến ngày: 13/10/2026":
            text.text = "đến ngày: 20/10/2026"
    for p in body.findall("w:p", NS):
        if "Ghi chú minh chứng:" in "".join(p.xpath(".//w:t/text()", namespaces=NS)):
            replacement = paragraph("Báo cáo tuần 6 tổng hợp các công việc đã thực hiện: bổ sung bộ lọc tìm kiếm công thức, hoàn thiện tiện ích gọi API và xử lý lỗi, cài đặt thư viện giao diện, xây dựng store, component và bố cục trang tìm kiếm. Các phần bổ sung sử dụng nền tảng chung của nhóm. Số liệu kiểm thử được ghi nhận ngày 14/10/2026; liên kết trong bảng dẫn đến nhánh làm việc, chưa phản ánh đầy đủ những thay đổi đang lưu trên máy.")
            body.replace(p, replacement)
    table = body.find("w:tbl", NS)
    rows = table.findall("w:tr", NS)
    row_template = deepcopy(rows[1])
    for row in rows[1:]:
        table.remove(row)
    for index, (title, details, limitation) in enumerate(ROWS, 1):
        row = deepcopy(row_template)
        # Xóa ID paragraph bản sao; Word tự sinh khi lưu.
        for element in row.iter():
            for attr in list(element.attrib):
                if etree.QName(attr).localname in ("paraId", "textId"):
                    del element.attrib[attr]
        cells = row.findall("w:tc", NS)
        replace_cell(cells[0], [paragraph(str(index), center=True)])
        replace_cell(cells[1], [paragraph(title, bold=True, keep=True),
            paragraph("Đã hoàn thành:", bold=True, color="4EA72E", keep=True),
            *[paragraph("• " + item) for item in details],
            paragraph("Hạn chế và phần cần hoàn thiện thêm:", bold=True, keep=True),
            paragraph(limitation)])
        # Cột nhánh giữ nguyên hyperlink từ mẫu; tỷ lệ chỉ đánh giá phạm vi của từng dòng.
        replace_cell(cells[3], [paragraph("100%", center=True)])
        table.append(row)
    header = table.find("w:tr", NS)
    trpr = header.find("w:trPr", NS)
    if trpr is None:
        trpr = etree.Element(f"{{{W}}}trPr")
        header.insert(0, trpr)
    if trpr.find("w:tblHeader", NS) is None:
        etree.SubElement(trpr, f"{{{W}}}tblHeader")
    for br in doc.findall(".//w:lastRenderedPageBreak", NS):
        br.getparent().remove(br)
    entries["word/document.xml"] = etree.tostring(doc, xml_declaration=True, encoding="UTF-8", standalone=True)
    if "docProps/core.xml" in entries:
        core = etree.fromstring(entries["docProps/core.xml"])
        dc = "http://purl.org/dc/elements/1.1/"
        for key, value in {"title": "Báo cáo Lab 6 – Chung Thiện Ý – 2312804", "creator": "Chung Thiện Ý"}.items():
            item = core.find(f"{{{dc}}}{key}")
            if item is None:
                item = etree.SubElement(core, f"{{{dc}}}{key}")
            item.text = value
        entries["docProps/core.xml"] = etree.tostring(core, xml_declaration=True, encoding="UTF-8", standalone=True)
    if "docProps/app.xml" in entries:
        app = etree.fromstring(entries["docProps/app.xml"])
        for item in list(app):
            if etree.QName(item).localname in ("Pages", "Words", "Characters", "CharactersWithSpaces", "Lines", "Paragraphs"):
                app.remove(item)  # Không giữ số trang/từ của Lab 5; Word tính lại khi mở.
        entries["docProps/app.xml"] = etree.tostring(app, xml_declaration=True, encoding="UTF-8", standalone=True)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with ZipFile(args.output, "w", ZIP_DEFLATED) as archive:
        for name, data in entries.items():
            archive.writestr(name, data)
    with ZipFile(args.output) as archive:
        assert archive.testzip() is None
        for name in archive.namelist():
            if name.endswith((".xml", ".rels")):
                etree.fromstring(archive.read(name))
    print(f"Created {args.output.name}: {len(ROWS)} task rows; XML/ZIP valid; template unchanged.")


if __name__ == "__main__":
    main()
