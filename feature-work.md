# WordUp Feature Work

Tai lieu nay la backlog phat trien cho du an WordUp, dua tren `foundation.txt` va cac mau giao dien trong thu muc `ui/`.

## 1. Muc tieu san pham

WordUp la ung dung WPF hoc tu vung tieng Anh bang flashcard, quiz va spaced repetition. Ung dung can co trai nghiem nhe, ro rang, tap trung vao viec hoc hang ngay va quan ly kho tu vung ca nhan.

## 2. Huong giao dien

Phong cach UI can bam sat mau trong `ui/`:

- Nen chinh: lavender/neutral rat nhat, tao cam giac sach va tap trung.
- Mau chu dao: indigo/xanh hoc thuat cho title, CTA, progress va selected tab.
- Card: nen trang, vien mong, bo goc nho, bong nhe.
- Header: top app bar co nut back/menu/profile/settings tuy man hinh.
- Navigation: bottom tab cho Dashboard, Study Mode, Word Manager, Analytics.
- Form: input cao vua phai, icon ben trai, validation hien thi bang text nho mau do.
- Button: primary indigo, secondary outline, destructive mau do nhat/do dam.
- Typography: title dam, body gon, label nho, khong dung chu qua lon trong card.
- Layout: uu tien dang mobile/tall nhu mau, nhung WPF can responsive khi cua so rong hon.

## 3. Kien truc de xuat

Su dung WPF voi cau truc tach lop don gian:

```text
WordUp/
  Models/
  Services/
  ViewModels/
  Views/
  Controls/
  Resources/
    Styles.xaml
    Colors.xaml
    Icons.xaml
```

- `Models`: User, Deck, VocabularyWord, QuizQuestion, StudySession, Achievement.
- `Services`: AuthenticationService, VocabularyService, SrsService, QuizService, ProgressService.
- `ViewModels`: moi man hinh co ViewModel rieng, dung binding thay vi xu ly UI truc tiep trong code-behind.
- `Views`: Login, Register, ForgotPassword, Dashboard, StudyMode, Quiz, QuizResult, WordManager, Analytics, Settings, Profile, About.
- `Controls`: AppHeader, BottomNavigation, PrimaryButton style, StatCard, DeckCard, WordTable, ProgressRing.

## 4. Man hinh can phat trien

### 4.1 Authentication

- Login view:
  - Logo/brand `VocabMaster Pro` hoac `WordUp`.
  - Tab Dang nhap / Dang ky.
  - Email, mat khau, quen mat khau.
  - Nut tiep tuc mau indigo.
- Register view:
  - Ho va ten, email, mat khau, xac nhan mat khau.
  - Validation bat buoc va dinh dang email.
- Forgot password:
  - Email input.
  - Nut gui yeu cau.
  - Link quay lai dang nhap.

### 4.2 Dashboard

- Loi chao theo ten nguoi dung.
- Badge avatar chu cai viet tat.
- Stat cards:
  - Tong so tu da thuoc.
  - Tu can on hom nay.
- Danh sach deck:
  - IELTS Essentials.
  - Daily English.
  - TOEFL Prep.
  - Moi deck co tien do va nut `Hoc ngay`.
- Nut them bo tu moi.
- Bottom navigation.

### 4.3 Study Mode

- Header co nut thoat, title, dem so cau hien tai.
- Progress bar `Mastery Progress`.
- Flashcard:
  - Mat truoc: word, IPA, nut phat am.
  - Mat sau: meaning, type, example, optional image.
- Interaction:
  - Click/tap de lat the.
  - Sau khi lat hien 3 muc danh gia: Chua thuoc, Binh thuong, Da thuoc.
- Goi `SrsService` de tinh lan on tiep theo.

### 4.4 Quiz Mode

- Header `Quiz Session`, settings icon.
- Progress cau hoi va phan tram.
- Card cau hoi hien thi tu vung.
- 4 lua chon A/B/C/D.
- Nut `Cau tiep theo`.
- Khi nop bai, chuyen sang Quiz Result.

### 4.5 Quiz Result

- Progress ring diem phan tram.
- So cau dung/tong cau.
- Danh sach review:
  - Cau dung mau xanh, icon check.
  - Cau sai mau do, icon x.
  - Hien dap an da chon va dap an dung.
- Nut hoan thanh quay ve Dashboard.

### 4.6 Word Manager

- Tab `Danh sach tu vung` va `Them/Sua tu`.
- Search input.
- Bang gom Word, Type, Meaning.
- Hanh dong:
  - Them tu.
  - Sua tu.
  - Xoa tu.
- Can co empty state khi chua co tu nao.

### 4.7 Analytics

- Today's Goal card:
  - Progress ring.
  - So tu da hoc/tong muc tieu.
- Weekly Progress:
  - Bar chart theo ngay.
- Achievements:
  - Badge `Linh moi`, `Cham chi`, `Hoc gia`.
  - Badge active dung indigo, inactive dung neutral.

### 4.8 Account, Settings, About

- Account:
  - Profile summary.
  - Edit Profile.
  - About.
  - General Settings.
  - Dark Theme toggle.
  - Delete Account.
  - Logout.
- Edit Profile:
  - Avatar, change photo.
  - Full name, email, phone.
  - Current password, new password, confirm password.
- Settings:
  - Text-to-speech volume slider.
  - Daily reminders toggle.
  - Auto-play audio toggle.
  - Offline mode toggle.
- About:
  - Mo ta ngan ve ung dung.
  - Danh sach doi ngu phat trien bang card.

## 5. Du lieu mau ban dau

Can co seed data de dev UI nhanh:

- User:
  - Name: Nguyen Van A
  - Email: student@university.edu
  - Level: Hoc gia
- Decks:
  - IELTS Essentials: 45/100
  - Daily English: 200/500
  - TOEFL Prep: 10/300
- Words:
  - Abysmal, adj., Very bad; appalling.
  - Benevolent, adj., Well meaning and kindly.
  - Cacophony, noun, A harsh, discordant mixture of sounds.
  - Ubiquitous, adj., Present everywhere.

## 6. Thu tu thuc hien

1. Tao resource UI dung chung: colors, typography, button, input, card.
2. Tao layout chinh: shell, header, bottom navigation.
3. Tao models va seed data service chay local.
4. Lam Authentication UI va dieu huong tam.
5. Lam Dashboard voi deck/stat cards.
6. Lam Study Mode va flashcard flip state.
7. Lam Quiz Mode va Quiz Result.
8. Lam Word Manager CRUD tren in-memory data truoc.
9. Lam Analytics voi progress ring/bar chart bang XAML.
10. Lam Settings/Profile/About.
11. Them validation va thong bao loi.
12. Chuyen data service sang luu tru local neu can: JSON hoac SQLite.

## 7. Tieu chi hoan thanh UI

- Moi man hinh co cung language visual voi thu muc `ui/`.
- Khong co text bi tran khoi button/card o kich thuoc cua so nho.
- Form co validation ro rang.
- Navigation giua cac man hinh hoat dong.
- Cac action CRUD co phan hoi UI.
- Du lieu mau hien thi du de test dashboard, study, quiz va analytics.

## 8. Ghi chu ky thuat

- Project hien dang target `net10.0-windows`; can dam bao may dev co SDK phu hop.
- Neu muon on dinh hon cho nhom, co the can nhac chuyen ve `net8.0-windows` neu moi nguoi chua co .NET 10 SDK.
- Khong nen dat nhieu logic trong `MainWindow.xaml.cs`; uu tien ViewModel/Service de de mo rong.
- Nen giu anh mau trong `ui/` lam reference, khong copy truc tiep vao UI neu khong can.
