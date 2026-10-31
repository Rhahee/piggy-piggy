# Piggy

**Versi: [0.1.0.2 — Ribbon and Ledger Polish](https://github.com/Rhahee/piggy-piggy/releases/tag/app-v0.1.0.2)**

**Jenis perubahan: Minor Fix.** Baseline awal tetap tersedia melalui [catatan rilis 0.1.0.0](docs/releases/0.1.0.0.md).

A portable, offline Windows application for personal finances across multiple bank accounts. English interface, magenta Office-style ribbon, Rupiah amounts (**Rp 1,250,000.00**), and dates in **MM/DD/YYYY**.

[Repository](https://github.com/Rhahee/piggy-piggy) · [Releases](https://github.com/Rhahee/piggy-piggy/releases) · [Verification](docs/VERIFICATION.md) · [Release guide](docs/RELEASING.md)

## Update 0.1.0.2 — Minor Fix

Update ini merapikan ribbon, alignment tabel, penanda interaksi dan pesan tabel kosong, serta memperbaiki bulan date picker dan sorting saat pencarian. Ctrl+F langsung memfokuskan pencarian setelah halaman siap. Lihat [catatan perubahan](RELEASE_NOTES.md).

## Update 0.1.0.1 — Minor Fix

Validasi isian kini mengarahkan fokus ke kolom yang perlu diperbaiki dan menggulirkannya ke area terlihat. Lokasi dokumen lengkap tersedia melalui tooltip pada status bar. Margin dan padding form dirapikan dengan kelipatan empat; saldo awal kosong tetap berarti nol.

Jendela utama sekarang memakai title bar, bingkai, dan tombol minimize/maximize/close bawaan Windows. Resize, maximize, restore, dan Snap ditangani OS; ketersediaan Snap mengikuti versi serta pengaturan Windows. Tombol **Save** berada di sisi kanan baris **File / Home / Data / View**, dengan tooltip **Save (Ctrl+S)**. Nama dokumen dan tanda **\*** untuk perubahan yang belum disimpan tetap tampil di title bar.

Tema isi aplikasi tetap memakai tampilan ribbon magenta. Dokumen `.mpb`, perhitungan keuangan, dan proses Save manual tetap kompatibel. Lihat [catatan update lengkap](RELEASE_NOTES.md).

Logo yang disetujui menampilkan babi magenta–putih yang memakan koin emas, dengan tulisan **Piggy**. Ikon aplikasi memakai koin emas saja. Layar awal kini terpusat dengan urutan **logo → New → Open**; versi dan tombol update tetap di kanan bawah. Lihat [aset dan panduan identitas](docs/BRANDING.md).

Nama yang tampil adalah **Piggy**. Nama berkas **Piggy - Piggy.exe** dan paket update tetap dipertahankan agar kompatibel dengan updater 0.1.0.0 yang sudah dirilis.

## Menjalankan

1. Ekstrak paket portable ke folder pilihanmu.
2. Buka **Piggy - Piggy.exe**. Tidak perlu instalasi atau memasang .NET.
3. Pilih **New** untuk dokumen kosong atau **Open** untuk membuka dokumen **.mpb**.
4. Tambahkan bank melalui **Data → Accounts**. Satu rekening per bank; nama bank harus unik. Isi tanggal dan saldo awal sebelum transaksi pertama pada tanggal tersebut.
5. Buat kategori melalui **Data → Categories**, lalu mulai mencatat **Income**, **Expense**, atau **Transfer**.

## Memakai dokumen yang sudah ada

Jika sudah memiliki dokumen keuangan dari build sebelumnya, simpan perubahan dengan **Save**, tutup aplikasi tersebut, ekstrak paket versi baru ke folder pilihanmu, lalu **Open** dokumen **.mpb** yang sama.

Format dokumen saat ini **9**, dan format 1–8 tetap bisa dibuka tanpa menulis ulang berkas asal sampai Save. Pembaruan setelah baseline 0.1.0.0 tersedia melalui tombol update aplikasi.

Repository ini dimulai dari baseline C#/WPF **0.1.0.0**. Riwayat Git lama, eksperimen, hasil build, dan dokumen keuangan pribadi tidak disertakan.

## Dokumen dan penyimpanan

- **New / Open / Save / Save As** bekerja seperti dokumen desktop. Perubahan disimpan manual dengan **Save** atau **Ctrl+S**.
- Saat menutup, membuat dokumen baru atau membuka dokumen lain, perubahan yang belum disimpan memunculkan **Save / Don't Save / Cancel**. Pembatalan atau kegagalan Save mempertahankan dokumen yang sedang dikerjakan.
- Lokasi dan nama berkas dipilih pengguna. Dokumen **.mpb** adalah JSON berversi, bukan berkas Excel, dan dibatasi 50 MB.
- Penyimpanan memakai berkas sementara dan penggantian atomik. Jika dokumen berubah dari luar aplikasi, Save menolak menimpanya; gunakan **Save As**.
- Dokumen tidak dienkripsi. Aplikasi tidak memakai login, koneksi bank, sinkronisasi, atau autosave. Internet dipakai untuk metadata rilis dan pengunduhan update yang diminta pengguna; isi dokumen tidak dikirim.
- Simpan salinan **.mpb** di lokasi cadangan pilihanmu. Cadangan EXE dari updater tidak mencadangkan dokumen keuangan.

## Input nominal dan tanggal

- **Rp 0.00** pada kolom kosong adalah petunjuk, bukan isi yang harus dihapus.
- Ketik angka langsung: **100000** menjadi **100,000** saat diketik dan **100,000.00** saat selesai mengisi.
- Koma memisahkan ribuan, titik memisahkan desimal. Maksimal dua angka desimal dan nominal **Rp 999,999,999,999,999.99**.
- Tanggal hanya memakai hari, dengan format **MM/DD/YYYY**. Transaksi historis boleh dicatat selama tidak mendahului saldo awal bank terkait. Saldo negatif memerlukan peringatan.

## Transaksi

- **Income / Expense** wajib berkategori. Description bersifat opsional. Admin Fee tersedia terpisah dan otomatis dicatat sebagai Expense dengan kategori sistem **Admin Fee**.
- Income **Before fee**: Amount adalah bruto; uang masuk bersih = Amount − Fee. Income **After fee**: Amount adalah uang bersih yang diterima; bruto = Amount + Fee.
- Expense mengurangi rekening sebesar Amount + Fee.
- **Transfer** memindahkan uang antarrekening sendiri. Rekening asal berkurang Amount + Fee, rekening tujuan bertambah Amount. Pokok transfer tidak dihitung sebagai Income/Expense.
- Baris Admin Fee terhubung dengan transaksi asal; edit atau hapus salah satunya akan bekerja pada transaksi beserta biaya terkait.
- **Overview** menunjukkan saldo per hari ini. Income/Expenses dan tabel mengikuti periode yang dipilih. Transaksi mendatang mulai memengaruhi saldo pada tanggalnya.

## Tabel, pencarian dan filter

- Type menggunakan panah Income, Expense dan Transfer. **Allocations** menggunakan ikon target; **Debt** menggunakan ikon uang. Hover menampilkan rincian lengkap.
- Teks panjang menggunakan elipsis dan tooltip lengkap. Sel Allocation/Debt tanpa kaitan dibiarkan kosong.
- **Ctrl+F** mencari tanggal, tipe, bank, kategori, Description, Purpose, nama hutang/lender dan nominal. **100000** maupun **100,000.00** dapat menemukan nilai yang sama; pencarian teks tetap tersedia.
- Filter membatasi bank, tipe, kategori, Purpose atau pelacakan hutang. **Clear Filters** mempertahankan periode yang sedang dipilih.

## Purpose dan Period

**Data → Allocation → Purpose** mengelola catatan alokasi transfer. Transfer dapat dibagi menjadi beberapa Purpose dan nominal. Purpose biasa merupakan informasi pelengkap; dana Wishlist dan pokok Debt dikelola melalui tautan fiturnya masing-masing.

**Data → Manage → Period** memilih Calendar Month, Custom Range atau Pay Cycle. Pay Cycle mendukung tanggal gajian yang tetap, dimajukan ke hari kerja, atau diundur saat weekend; koreksi hari gajian aktual dapat dicatat. Hari libur nasional tidak dihitung otomatis. Pilihan periode bertahan sampai pengguna menggantinya dan ikut disimpan dengan Save.

## Wishlist

Wishlist menggunakan Split View, dengan daftar item dan detail alokasi/aktivitas. Wishlist dapat dibuat tanpa alokasi awal. Alokasi dicatat per item dan bank, dapat ditambah, dipindahkan atau dilepas. Admin Fee memakai saldo bebas dan tidak mengambil alokasi Wishlist.

**Balance** tetap menunjukkan saldo rekening, sedangkan **Available Balance** mengurangi dana yang sudah dialokasikan. Pembelian menggunakan dana Wishlist pada satu bank yang cukup; gabungkan dananya terlebih dahulu jika tersebar. Status **Purchased** bertahan sampai pengguna menutup Wishlist. Sisa alokasi dapat dilepas melalui balancing. Aktivitas dan tanggal dapat dikoreksi melalui alur yang disediakan, dengan validasi saldo historis.

## Debt dan Self Debt

- **Debt kepada lender** dimulai dari Income yang ditandai **Track as debt**. Expense tertaut membayar pokok. Kategori tetap wajib; Admin Fee tidak mengurangi pokok.
- **Self Debt** mencatat penggunaan dana sendiri dari rekening asal. Expense tertaut atau alokasi transfer **Borrow as Self Debt** menambah pokok yang harus dikembalikan.
- Pembayaran Self Debt dapat melalui Income yang ditandai atau alokasi transfer **Repay Self Debt**. Pengembalian harus menuju rekening asal; nama Purpose saja tidak membuat tautan hutang.
- Sisa nol menjadi **Pending Confirmation** sampai pengguna memilih **Mark as Paid**. Hutang Paid harus **Reopen Debt** sebelum menerima tambahan pinjaman.
- Edit/hapus transaksi memvalidasi kembali riwayat hutang. **Remove Tracking** melepas tautan sambil mempertahankan transaksi bank. Transaksi Wishlist tidak sekaligus ditautkan ke Debt.

## Pembaruan

Tombol ikon update berada di kanan bawah. Hijau berarti versi terkonfirmasi terbaru; tanda **!** berarti update tersedia. Setiap klik memeriksa ulang metadata rilis.

**Update and Restart** mengunduh ZIP dan checksum, memeriksa SHA-256 serta versi EXE, kemudian mengganti EXE pada lokasi yang sama dan membuka kembali dokumen terakhir. Perubahan dokumen harus disimpan dahulu. Pembatalan atau kegagalan tidak mengganti aplikasi lama. Folder portable harus dapat ditulis pengguna; aplikasi tidak meminta hak administrator.

Pemeriksaan update hanya mengakses **Rhahee/piggy-piggy**. Sebelum rilis pertama dipublikasikan, aplikasi dapat menampilkan informasi bahwa rilis belum tersedia. Update baru ditawarkan setelah paket dan checksum tersedia.

## Penomoran versi

Format: **Release.Feature.Fix.Minor**. Kenaikan satu bagian mengembalikan seluruh angka di kanannya ke 0:

- Minor fix: **0.1.0.1**.
- Perbaikan fitur: **0.1.1.0**.
- Penambahan fitur: **0.2.0.0**.
- Target rilis stabil: **1.0.0.0**.

Klasifikasi perubahan dan nomor versi dikonfirmasikan sebelum rilis. Bentuk portable merupakan pilihan distribusi; 0 menandai tahap pengembangan.

## Build dan pemeriksaan

Memerlukan Windows x64 dan .NET SDK 10. Jalankan **./publish.ps1** untuk tes inti, pemeriksaan WPF dan build EXE portable. Parameter **-Dotnet** menerima lokasi SDK bila tidak tersedia di PATH. Hasil paket berada di **dist/**.

Lihat [panduan folder proyek](docs/FOLDER_GUIDE.md) untuk membedakan sumber kode, aplikasi portable, cache build, dokumen dan bahan pengujian.

GitHub Actions memeriksa tag/versi, menjalankan pemeriksaan, membangun paket dan checksum, lalu membuat draft release. Publikasi dan promosi kanal dilakukan setelah verifikasi; lihat [Release guide](docs/RELEASING.md).

Ikon berasal dari Microsoft Fluent UI System Icons; lisensi disertakan pada **Assets/Icons/LICENSE.txt** dan paket portable.
