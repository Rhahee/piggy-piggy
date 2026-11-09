# Pemeriksaan installer preview — 9 November 2026

Hasil: **dua bug terkonfirmasi**. Paket asli tidak dibangun ulang atau diganti selama pemeriksaan.

Tindak lanjut: kedua temuan sudah ditangani pada kode web installer/startup aplikasi, dengan tes downgrade dan registry terisolasi. Lihat [implementasi dan batas rollout](../INSTALLER.md). Paket preview lama yang diperiksa di bawah tetap merupakan artefak historis dan masih memiliki dua bug tersebut. Nomor baris di laporan ini mengacu pada sumber saat pemeriksaan preview, bukan skrip web installer saat ini.

Paket yang diperiksa: `Piggy-Piggy-0.1.0.2-installer-preview-win-x64.exe`.
SHA-256: `ccf46aae5776c3ba0501d479187b01cc641bfb3177ac3b60f7e5c40b329a90b7`.

## Temuan

### P2 — Installer lama dapat menurunkan versi tanpa perlindungan

Lokasi: `installer/Piggy.iss`, entri EXE di bagian `[Files]` (baris 41).

Installer memakai `ignoreversion` dan tidak memiliki pemeriksaan versi EXE terpasang. Setelah helper updater mengganti EXE menjadi build lokal **0.1.0.3**, menjalankan installer **0.1.0.2** kembali menghasilkan exit code **0** dan menurunkan EXE ke **0.1.0.2**. Versi 0.1.0.3 hanya fixture pengujian, bukan rilis publik. Flag tersebut memang mengabaikan perbandingan versi sesuai [dokumentasi Inno Setup](https://jrsoftware.org/ishelp/topic_filessection.htm).

Dampak: pengguna dapat kehilangan perbaikan yang sudah diperoleh melalui updater ketika menjalankan ulang installer lama. Risiko kompatibilitas dokumen versi mendatang belum diuji dan tidak dinyatakan sebagai kerusakan yang sudah terjadi.

Perbaikan yang disarankan: bandingkan empat komponen versi EXE pada lokasi tujuan sebelum pemasangan dan tolak downgrade, termasuk pemasangan silent. Jangan hanya mengandalkan DisplayVersion registry karena metadata tersebut bisa tertinggal setelah updater berjalan.

### P3 — Versi di daftar aplikasi Windows tertinggal setelah update

Lokasi: `PortableUpdater.cs`, pemanggilan `UpdatePackage.Replace` (baris 83); `installer/Piggy.iss`, `AppVersion` (baris 14).

Helper updater benar-benar mengganti dan menjalankan kembali EXE terpasang. Jendela startup memperlihatkan **v0.1.0.3** dan hash EXE cocok dengan fixture, sedangkan `DisplayVersion` pada registrasi uninstall tetap **0.1.0.2**. Updater mengganti EXE tanpa menyelaraskan metadata instalasi.

Dampak: daftar aplikasi Windows menampilkan versi lama, sehingga informasi untuk pemeriksaan versi dan dukungan menjadi keliru. Aplikasi tetap bisa berjalan.

Perbaikan yang disarankan: setelah update berhasil, selaraskan DisplayVersion hanya untuk instalasi Piggy yang AppId dan lokasi EXE-nya cocok. Portable tidak perlu membuat registrasi uninstall.

## Pengujian dan batas cakupan

Instalasi Piggy pengguna sudah ditemukan di `%LOCALAPPDATA%\Programs\Piggy`. Pengujian mutasi memakai salinan skrip yang mengganti AppId, nama tampilan, lokasi default, ikon path compiler dan nama shortcut menjadi identitas QA. Payload EXE dan aturan instalasi lainnya tetap sama. Ini pengujian perilaku skrip melalui paket QA, bukan pemasangan ulang paket asli di atas instalasi pengguna.

**22 assertion lulus**, termasuk assertion yang mengonfirmasi dua bug di atas. Skenario yang berhasil: navigasi wizard sampai Ready to Install; pembatalan sebelum pemasangan tanpa payload/registrasi; instalasi baru; shortcut Desktop tidak dibuat ketika tidak dipilih; kecocokan hash EXE; helper updater dengan paket lokal; restart ke versi fixture; pemasangan ulang mempertahankan lokasi; penambahan shortcut Desktop; dan uninstall menghapus payload, registrasi dan kedua shortcut QA. Sentinel `.mpb` tetap sama setelah downgrade dan uninstall. Backup EXE updater tertinggal sesuai perilaku preview yang didokumentasikan.

Pembaruan memakai manifest pengujian dan helper produksi langsung, dengan parent yang sudah tidak ada; tidak menguji tombol update, pengunduhan HTTP atau handshake penutupan aplikasi. Sentinel `.mpb` bukan dokumen keuangan valid, sehingga hasil pelestarian berkas tidak menggantikan tes Save/Open. Tidak ada dokumen pribadi yang dibuka. Skenario dokumen belum disimpan saat instalasi, pembatalan di tengah ekstraksi, akun Windows standar terpisah, dan mesin Windows bersih belum diuji.

Log dan skrip reproduksi lokal (diabaikan Git):

- `qa/archive/installer-preview-2026-11-09/installer-review/Review-Install.ps1` dan `review-install.log`.
- `qa/archive/installer-preview-2026-11-09/installer-review/Review-Finish.ps1` dan `review-finish.log`.
- `qa/archive/installer-preview-2026-11-09/installer-review/install.log`, `downgrade.log`, dan `uninstall.log`.

Semua jendela dan registrasi QA telah ditutup/dicopot. Folder QA masih menyimpan sentinel fiktif serta backup EXE untuk bukti. Kode produksi, versi proyek, kanal update dan paket preview asli tidak diubah oleh pemeriksaan ini.
