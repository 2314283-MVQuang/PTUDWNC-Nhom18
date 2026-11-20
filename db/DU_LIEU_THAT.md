# Dữ liệu thật — 10 danh mục, 50 công thức

Thay cho dữ liệu ngẫu nhiên (Bogus) trước đây theo yêu cầu giảng viên: *ít nhất 10 category, 50 công thức, mỗi công thức có đầy đủ các bước chế biến, nguyên liệu, hình ảnh minh hoạ; xoá hết dữ liệu ngẫu nhiên để test.*

- Nội dung công thức: nhóm 18 biên soạn (món ăn Việt Nam phổ biến, định lượng và các bước thực tế).
- Ảnh: ảnh thật trên Wikimedia Commons, giấy phép tự do (Public domain, CC0, CC BY, CC BY-SA). Tác giả và giấy phép ghi trong bảng dưới và ở mục "Hướng dẫn bổ sung" của từng công thức.
- Nạp tự động bởi `RealDataSeeder` (src/CulinaryBlog.Infrastructure/Persistence/Seed) khi `Seed:RealData = true`: tải ảnh một lần về MinIO, tạo danh mục/công thức theo slug, ẩn (xoá mềm) công thức ngẫu nhiên của `DbSeeder` và 3 công thức mẫu của `03-seed.sql`. Chạy lại nhiều lần không sinh trùng.

| # | Công thức | Danh mục | Nguyên liệu | Bước | Ảnh (tác giả, giấy phép) |
|---:|---|---|---:|---:|---|
| 1 | Canh chua cá lóc | Món Canh, Soup | 13 | 6 | [Phương Huy](https://commons.wikimedia.org/wiki/File:Canh_chua_c%C3%A1_l%C3%B3c_%E1%BB%9F_Th%E1%BB%A7y_Tr%C3%BAc_Qu%C3%A1n%2C_%C4%91%C6%B0%E1%BB%9Dng_Nguy%E1%BB%85n_Nh%E1%BB%AF_L%C3%A3m_%281%29.jpg), Public domain |
| 2 | Canh bí đao nấu tôm | Món Canh, Soup | 9 | 5 | [Phương Huy](https://commons.wikimedia.org/wiki/File:B%E1%BB%AFa_c%C6%A1m_nh%C3%A0_Mheo%2C_ng19th1n2020_%28n%E1%BB%93i_canh_b%C3%AD_%C4%91ao%29.jpg), CC BY-SA 4.0 |
| 3 | Canh bắp cải cuộn thịt | Món Canh, Soup | 9 | 5 | [Nguyen Thi Quynh Tram](https://commons.wikimedia.org/wiki/File:Canh_bap_cai_cuon_thit.jpg), CC BY 3.0 |
| 4 | Canh khổ qua nhồi thịt | Món Canh, Soup | 10 | 5 | [HungryHuy](https://commons.wikimedia.org/wiki/File:Soup_made_from_stuffed_bitter_melon%2C_Canh_Kho_Qua.jpg), CC BY 2.0 |
| 5 | Súp cua | Món Canh, Soup | 11 | 6 | [Phương Huy](https://commons.wikimedia.org/wiki/File:Nh%C3%A0_m%C3%ACnh_L%E1%BB%85_30th4n2023_%28s%C3%BAp_cua_rong_bi%E1%BB%83n_c%C3%A0_r%E1%BB%91t%29_%281%29.jpg), CC BY-SA 4.0 |
| 6 | Rau muống xào tỏi | Món Chiên, Xào, Áp Chảo | 6 | 5 | [Phương Huy](https://commons.wikimedia.org/wiki/File:Rau_mu%E1%BB%91ng_x%C3%A0o_t%E1%BB%8Fi%2C_th%C3%A1ng_4_n%C4%83m_2018.jpg), Public domain |
| 7 | Bò lúc lắc | Món Chiên, Xào, Áp Chảo | 11 | 5 | [Nick Nguyen](https://commons.wikimedia.org/wiki/File:Bo_Luc_Lac-_the_shaking_beef.jpg), CC BY-SA 2.0 |
| 8 | Chả giò (nem rán) | Món Chiên, Xào, Áp Chảo | 12 | 6 | [Phương Huy](https://commons.wikimedia.org/wiki/File:Ch%E1%BA%A3_gi%C3%B2_SG_%28nem_r%C3%A1n_gi%C3%B2n%29_%E1%BB%9F_qu%C3%A1n_b%C3%BAn_ri%C3%AAu_B%C3%A0_Ti_ng26th7n2023_%281%29.jpg), CC BY-SA 4.0 |
| 9 | Đậu hũ sốt cà chua | Món Chiên, Xào, Áp Chảo | 9 | 5 | [Mickey Đại Phát](https://commons.wikimedia.org/wiki/File:Tomato-sauced_tofu.jpg), CC BY-SA 4.0 |
| 10 | Cánh gà chiên nước mắm | Món Chiên, Xào, Áp Chảo | 9 | 5 | [Phương Huy](https://commons.wikimedia.org/wiki/File:C%C3%A1nh_g%C3%A0_chi%C3%AAn_n%C6%B0%E1%BB%9Bc_m%E1%BA%AFm%2C_th%C3%A1ng_8_n%C4%83m_2018_%281%29.JPG), Public domain |
| 11 | Gà luộc lá chanh | Món Hấp, Luộc | 7 | 5 | [Sjschen](https://commons.wikimedia.org/wiki/File:BeiQieJi-WhiteCutChicken.jpg), CC BY-SA 3.0 |
| 12 | Cá hấp xì dầu | Món Hấp, Luộc | 9 | 5 | [Geoffreyrabbit](https://commons.wikimedia.org/wiki/File:Steamed_fish_with_soy_sauce.jpg), CC BY-SA 4.0 |
| 13 | Tôm hấp nước dừa | Món Hấp, Luộc | 7 | 5 | [Phương Huy](https://commons.wikimedia.org/wiki/File:Nh%C3%A0_m%C3%ACnh_L%E1%BB%85_30th4n2023_%28m%C3%B3n_t%C3%B4m_h%E1%BA%A5p%29_%282%29.jpg), CC BY-SA 4.0 |
| 14 | Bánh cuốn nhân thịt | Món Hấp, Luộc | 11 | 6 | [Casablanca1911](https://commons.wikimedia.org/wiki/File:B%C3%A1nh_cu%E1%BB%91n_nh%C3%A2n_th%E1%BB%8Bt.JPG), Public domain |
| 15 | Xôi gấc | Món Hấp, Luộc | 7 | 6 | [Nguyễn Thanh Quang](https://commons.wikimedia.org/wiki/File:X%C3%B4i_g%E1%BA%A5c.JPG), Public domain |
| 16 | Sườn nướng mật ong | Món Nướng | 11 | 5 | [Phương Huy](https://commons.wikimedia.org/wiki/File:C%C6%A1m_t%E1%BA%A5m_SG_l%E1%BB%81_%C4%91%C6%B0%E1%BB%9Dng_%28c%C6%A1m_s%C6%B0%E1%BB%9Dn%2C_mi%E1%BA%BFng_s%C6%B0%E1%BB%9Dn_n%C6%B0%E1%BB%9Bng_m%E1%BA%ADt_ong%29_ng30th7n2022_%284%29.jpg), CC0 |
| 17 | Đùi gà nướng sả | Món Nướng | 10 | 5 | [Phương Huy](https://commons.wikimedia.org/wiki/File:C%C6%A1m_g%C3%A0_s%C6%B0%E1%BB%9Dn_n%C6%B0%E1%BB%9Bng%2C_ng23th8n2022_%28%C4%90%C3%B9i_g%C3%A0_n%C6%B0%E1%BB%9Bng%29_%281%29.jpg), CC0 |
| 18 | Chạo tôm | Món Nướng | 10 | 6 | [SauceSupreme](https://commons.wikimedia.org/wiki/File:Ch%E1%BA%A1o_t%C3%B4m.jpg), CC BY 2.0 |
| 19 | Nem nướng Nha Trang | Món Nướng | 10 | 6 | [Index Librorum Permissorum](https://commons.wikimedia.org/wiki/File:Nem_Nuong_2018-07-29.jpg), CC0 |
| 20 | Bánh tráng nướng | Món Nướng | 9 | 5 | [Huutai11791](https://commons.wikimedia.org/wiki/File:Pizzabanhtrangnuong1.jpg), CC BY-SA 3.0 |
| 21 | Thịt kho trứng | Món Kho, Rim | 10 | 6 | [Baoothersks](https://commons.wikimedia.org/wiki/File:Th%E1%BB%8Bt_kho_h%E1%BB%99t_v%E1%BB%8Bt.jpg), CC BY-SA 4.0 |
| 22 | Cá kho tộ | Món Kho, Rim | 11 | 5 | [Binh Giang](https://commons.wikimedia.org/wiki/File:C%C3%A1_kho_t%E1%BB%99.JPG), Public domain |
| 23 | Bò kho | Món Kho, Rim | 12 | 6 | [Jason Hutchens](https://commons.wikimedia.org/wiki/File:Bo_Kho.jpg), CC BY 2.0 |
| 24 | Gà kho sả | Món Kho, Rim | 10 | 5 | [Phương Huy](https://commons.wikimedia.org/wiki/File:Nh%C3%A0_m%C3%ACnh_d%E1%BB%8Bp_L%E1%BB%85_2th9n2023_%28g%C3%A0_kho_s%E1%BA%A3%29_%281%29.jpg), CC BY-SA 4.0 |
| 25 | Tôm rim thịt ba chỉ | Món Kho, Rim | 9 | 5 | [Phương Huy](https://commons.wikimedia.org/wiki/File:T%E1%BA%BFt_2024_nh%C3%A0_m%C3%ACnh_%28rim_t%C3%B4m_th%E1%BB%8Bt%29_%281%29.jpg), CC0 |
| 26 | Phở bò | Bún, Phở, Mì | 13 | 6 | [Andy Li](https://commons.wikimedia.org/wiki/File:Beef_noodle_soup_%28Ph%E1%BB%9F_b%C3%B2%29_-_Pho_Hanoi_Authentic_2024-12-01.jpg), CC0 |
| 27 | Bún bò Huế | Bún, Phở, Mì | 12 | 6 | [Minh28397](https://commons.wikimedia.org/wiki/File:B%C3%BAn_b%C3%B2_Hu%E1%BA%BF_minh28397.jpg), CC BY-SA 4.0 |
| 28 | Bún chả Hà Nội | Bún, Phở, Mì | 12 | 5 | [Phương Huy](https://commons.wikimedia.org/wiki/File:B%C3%BAn_ch%E1%BA%A3_H%C3%A0_N%E1%BB%99i_%28th%C3%A1ng_7_n%C4%83m_2018%29_%281%29.jpg), CC BY-SA 4.0 |
| 29 | Mì Quảng tôm thịt | Bún, Phở, Mì | 11 | 6 | [Christopher Crouzet](https://commons.wikimedia.org/wiki/File:M%C3%AC_Qu%E1%BA%A3ng%2C_Da_Nang%2C_Vietnam.jpg), CC BY-SA 4.0 |
| 30 | Hủ tiếu Nam Vang | Bún, Phở, Mì | 12 | 6 | [Tonbi ko](https://commons.wikimedia.org/wiki/File:H%E1%BB%A7_ti%E1%BA%BFu_Nam_Vang_S%C3%A0i_G%C3%B2n.jpg), CC BY-SA 4.0 |
| 31 | Gỏi cuốn tôm thịt | Gỏi, Cuốn, Nộm | 10 | 5 | [Tran Hai Duong](https://commons.wikimedia.org/wiki/File:G%E1%BB%8Fi_cu%E1%BB%91n.jpg), CC0 |
| 32 | Gỏi đu đủ khô bò | Gỏi, Cuốn, Nộm | 10 | 5 | [stu_spivack](https://commons.wikimedia.org/wiki/File:G%E1%BB%8Fi_%C4%91u_%C4%91%E1%BB%A7_kh%C3%B4_b%C3%B2.jpg), CC BY-SA 2.0 |
| 33 | Gỏi xoài tôm khô | Gỏi, Cuốn, Nộm | 9 | 5 | [Ruth Hartnup](https://commons.wikimedia.org/wiki/File:G%E1%BB%8Fi_Xo%C3%A0i_at_Anh_and_Chi.jpg), CC BY 2.0 |
| 34 | Nộm hoa chuối | Gỏi, Cuốn, Nộm | 9 | 5 | [Jicara Foodie Traveller](https://commons.wikimedia.org/wiki/File:N%E1%BB%99m_hoa_chu%E1%BB%91i.png), CC BY-SA 4.0 |
| 35 | Bò bía | Gỏi, Cuốn, Nộm | 10 | 5 | [Susan](https://commons.wikimedia.org/wiki/File:Bo_Bia_Jicama_Rolls.jpg), CC BY 2.0 |
| 36 | Đậu hũ kho nấm | Món Chay | 10 | 5 | [Phương Huy](https://commons.wikimedia.org/wiki/File:Tr%C3%BAt_%C4%91%E1%BA%ADu_h%C5%A9_kho_t%E1%BB%AB_ch%E1%BA%A3o%2C_ng16th7n2022.jpg), CC0 |
| 37 | Chả giò chay | Món Chay | 11 | 5 | [Nguyen Thi Quynh Tram](https://commons.wikimedia.org/wiki/File:Chagiochay.JPG), CC BY 3.0 |
| 38 | Mì Quảng chay | Món Chay | 11 | 6 | [Phương Huy](https://commons.wikimedia.org/wiki/File:M%C3%AC_Qu%E1%BA%A3ng_chay%2C_th%C3%A1ng_9_n%C4%83m_2018_%281%29.jpg), Public domain |
| 39 | Canh rong biển đậu hũ chay | Món Chay | 9 | 5 | [Phương Huy](https://commons.wikimedia.org/wiki/File:M%C3%B3n_%C4%83n_c%C3%BAng_%C4%91%C6%B0a%2C_T%E1%BA%BFt_2022_%28canh_rong_bi%E1%BB%83n_chay%29.jpg), CC BY-SA 4.0 |
| 40 | Nấm xào chay | Món Chay | 10 | 5 | [Phương Huy](https://commons.wikimedia.org/wiki/File:M%C3%B3n_%C4%83n_c%C3%BAng_%C4%91%C6%B0a%2C_T%E1%BA%BFt_2022_%28m%C3%B3n_n%E1%BA%A5m_x%C3%A0o_chay%29_%281%29.jpg), CC BY-SA 4.0 |
| 41 | Chè đậu xanh nha đam | Chè, Tráng Miệng | 6 | 5 | [Baoothersks](https://commons.wikimedia.org/wiki/File:Ch%C3%A8_%C4%91%E1%BA%ADu_xanh_nha_%C4%91am.jpg), CC BY-SA 4.0 |
| 42 | Chè chuối | Chè, Tráng Miệng | 8 | 5 | [Phương Huy](https://commons.wikimedia.org/wiki/File:Ch%C3%A8_chu%E1%BB%91i_20201125.jpg), CC BY-SA 4.0 |
| 43 | Bánh flan | Chè, Tráng Miệng | 6 | 6 | [Cary Bass](https://commons.wikimedia.org/wiki/File:Homemade_Flan.jpg), CC BY-SA 2.5 |
| 44 | Chè bắp | Chè, Tráng Miệng | 8 | 6 | [Zxcvasdfqwer888](https://commons.wikimedia.org/wiki/File:Ch%C3%A8_B%E1%BA%AFp.jpg), CC BY-SA 3.0 |
| 45 | Chè khúc bạch | Chè, Tráng Miệng | 8 | 6 | [Phương Huy](https://commons.wikimedia.org/wiki/File:Ch%C3%A8_kh%C3%BAc_b%E1%BA%A1ch_%E1%BB%9F_qu%C3%A1n_Th%E1%BA%A3o_Vy_2020_09_06.jpg), CC BY-SA 4.0 |
| 46 | Cà phê sữa đá | Đồ Uống | 4 | 5 | [Clarin](https://commons.wikimedia.org/wiki/File:Ca_Phe_Sua_Da.jpg), CC BY 2.5 |
| 47 | Sinh tố bơ | Đồ Uống | 4 | 4 | [Hoangkid](https://commons.wikimedia.org/wiki/File:Sinh_t%E1%BB%91_b%C6%A1.jpg), Public domain |
| 48 | Trà tắc | Đồ Uống | 6 | 4 | [Cohaithaofoody](https://commons.wikimedia.org/wiki/File:Tr%C3%A0_t%E1%BA%AFc_v%C3%A0_b%C3%A1nh_tr%C3%A1ng.jpg), CC BY-SA 4.0 |
| 49 | Nước mía tắc | Đồ Uống | 3 | 4 | [Food Trails](https://commons.wikimedia.org/wiki/File:Sugarcanejuice.jpg), CC BY-SA 2.0 |
| 50 | Cà phê trứng | Đồ Uống | 5 | 5 | [Baoothersks](https://commons.wikimedia.org/wiki/File:C%C3%A0_ph%C3%AA_tr%E1%BB%A9ng_%28egg_coffee%29.jpg), CC BY-SA 4.0 |

## Ảnh danh mục

| Danh mục | Ảnh (tác giả, giấy phép) |
|---|---|
| Món Canh, Soup | [Phương Huy](https://commons.wikimedia.org/wiki/File:Canh_chua_c%C3%A1_l%C3%B3c_%E1%BB%9F_Th%E1%BB%A7y_Tr%C3%BAc_Qu%C3%A1n%2C_%C4%91%C6%B0%E1%BB%9Dng_Nguy%E1%BB%85n_Nh%E1%BB%AF_L%C3%A3m_%281%29.jpg), Public domain — chỉ dùng khi danh mục chưa có ảnh |
| Món Chiên, Xào, Áp Chảo | [Nick Nguyen](https://commons.wikimedia.org/wiki/File:Bo_Luc_Lac-_the_shaking_beef.jpg), CC BY-SA 2.0 — chỉ dùng khi danh mục chưa có ảnh |
| Món Hấp, Luộc | [Phương Huy](https://commons.wikimedia.org/wiki/File:Nh%C3%A0_m%C3%ACnh_L%E1%BB%85_30th4n2023_%28m%C3%B3n_t%C3%B4m_h%E1%BA%A5p%29_%282%29.jpg), CC BY-SA 4.0 — chỉ dùng khi danh mục chưa có ảnh |
| Món Nướng | [Phương Huy](https://commons.wikimedia.org/wiki/File:C%C6%A1m_t%E1%BA%A5m_SG_l%E1%BB%81_%C4%91%C6%B0%E1%BB%9Dng_%28c%C6%A1m_s%C6%B0%E1%BB%9Dn%2C_mi%E1%BA%BFng_s%C6%B0%E1%BB%9Dn_n%C6%B0%E1%BB%9Bng_m%E1%BA%ADt_ong%29_ng30th7n2022_%284%29.jpg), CC0 — chỉ dùng khi danh mục chưa có ảnh |
| Món Kho, Rim | [Viethavvh](https://commons.wikimedia.org/wiki/File:Th%E1%BB%8Bt_kho_T%C3%A0u.jpg), CC BY-SA 4.0 — chỉ dùng khi danh mục chưa có ảnh |
| Bún, Phở, Mì | [Kritzolina](https://commons.wikimedia.org/wiki/File:Ph%E1%BB%9F_b%C3%B2_in_a_German_restaurant_02.jpg), CC BY-SA 4.0 — chỉ dùng khi danh mục chưa có ảnh |
| Gỏi, Cuốn, Nộm | [Phuongnhu](https://commons.wikimedia.org/wiki/File:Goi_cuon_Phuongnhu.JPG), CC BY-SA 3.0 — chỉ dùng khi danh mục chưa có ảnh |
| Món Chay | [Paul R. Burley](https://commons.wikimedia.org/wiki/File:G%E1%BB%8Fi_Cu%E1%BB%91n_Chay_Vietnamese_Fresh_Vegetarian_Spring_Roll_2019-1599.jpg), CC BY-SA 4.0 — chỉ dùng khi danh mục chưa có ảnh |
| Chè, Tráng Miệng | [Tokeisan](https://commons.wikimedia.org/wiki/File:Ch%C3%A8_Th%E1%BA%A1ch_Tr%E1%BA%AFng_%C4%90%E1%BA%ADu_Xanh_Nh%C3%A3n_Nh%E1%BB%A5c.JPG), Public domain — chỉ dùng khi danh mục chưa có ảnh |
| Đồ Uống | [하나두리](https://commons.wikimedia.org/wiki/File:C%C3%A0_ph%C3%AA_s%E1%BB%AFa_%C4%91%C3%A1.jpg), CC BY 2.0 KR — chỉ dùng khi danh mục chưa có ảnh |
