# Piggy Web Installer

`Piggy-Piggy-Web-Setup-win-x64.exe` adalah installer tetap untuk Windows x64. Installer tidak membawa versi aplikasi tertentu: setiap pemasangan membaca kanal resmi `Rhahee/piggy-piggy/main/update-channel.json`, memverifikasi rilis publik yang ditunjuk, lalu mengunduh paket ZIP dan checksum-nya dari GitHub.

Rilis aplikasi berikutnya tidak memerlukan build ulang installer selama protokol kanal/paket dan mekanisme pemasangan tetap kompatibel. Perbaikan installer atau perubahan protokol tetap memerlukan installer baru. Versi internal launcher `1.0.0.0` terpisah dari versi aplikasi Piggy; ini tidak menerbitkan Piggy 1.0.

## Perilaku pemasangan

- Internet diperlukan. Tidak ada fallback ke aplikasi lama yang dibundel.
- Metadata, ZIP, checksum SHA-256, dan versi EXE diverifikasi di folder sementara sebelum payload dipasang. Hanya EXE, README, dan lisensi ikon dengan nama persis yang dikeluarkan dari ZIP.
- Paket yang diikuti adalah kanal rilis resmi, bukan GitHub Latest, draft, atau prerelease.
- Tujuan standar: `%LOCALAPPDATA%\Programs\Piggy`, per pengguna tanpa permintaan administrator atau pemasangan .NET terpisah.
- Shortcut Start Menu, shortcut Desktop opsional, dan entri uninstall Windows.
- Pemasangan ulang mempertahankan folder sebelumnya dan boleh memperbaiki versi yang sama. Versi EXE lebih baru di lokasi tujuan menolak downgrade, termasuk dalam mode silent.
- Tutup Piggy sebelum memasang ulang. Build aplikasi baru memiliki mutex yang dikenali installer/uninstaller. Installer tidak memaksa menutup aplikasi atau membuang perubahan dokumen.
- Versi di daftar aplikasi Windows diambil dari paket yang diunduh. Pada kode aplikasi baru, startup menyelaraskan DisplayVersion setelah update jika AppId dan lokasi EXE tepat cocok dengan registrasi terpasang. Aplikasi portable tidak membuat registrasi uninstall.
- Dokumen `.mpb` tetap dibuka/disimpan melalui aplikasi; belum ada asosiasi ekstensi. Uninstaller tidak menghapus dokumen atau folder secara rekursif. Backup EXE updater dapat tersisa.

Jika metadata tidak tersedia, koneksi gagal, unduhan dibatalkan, checksum salah, atau versi tidak cocok, setup berhenti sebelum pemasangan payload. Kegagalan saat tahap penyalinan ditangani mekanisme installer; interupsi daya/OS belum diuji.

## Build

Prasyarat pembuat: Windows x64, .NET SDK 10, dan [Inno Setup 6.7+](https://jrsoftware.org/isdl.php). Compiler tidak diunduh otomatis.

```powershell
.\build-installer.ps1 -Dotnet "$env:USERPROFILE\.dotnet10\dotnet.exe"
```

Gunakan `-Iscc 'C:\path\ISCC.exe'` bila compiler berada di lokasi lain. Skrip menjalankan pemeriksaan inti/WPF, membangun helper self-contained, mengompilasi web installer dan menulis SHA-256.

Helper perantara berada di `obj/installer-helper/win-x64/`. Gunakan `-OutputDirectory 'C:\path\output'` untuk verifikasi build di lokasi lain tanpa mengganti installer aktif.

Hasil: `dist/installer/Piggy-Piggy-Web-Setup-win-x64.exe` beserta `.sha256`, sekitar 24,7 MiB. Helper hanya diekstrak sementara; tidak menjadi payload aplikasi.

Implementasi memakai [AppVersion dinamis](https://jrsoftware.org/ishelp/topic_setup_appversion.htm), [berkas external](https://jrsoftware.org/ishelp/topic_filessection.htm) dan halaman unduh bawaan Inno Setup. Tidak ada endpoint pengujian atau URL alternatif dalam installer produksi.

## Verifikasi — 9 November 2026

**444 pemeriksaan inti dan 45 pemeriksaan WPF lulus.** Pemeriksaan baru mencakup pergantian kanal tanpa rebuild, penolakan rilis tidak lengkap/offline/draft, checksum salah, ketidakcocokan versi EXE, ekstraksi nama berkas persis, serta sinkronisasi registry dalam sandbox tanpa menyentuh instalasi pengguna.

**20 assertion integrasi lulus** pada paket QA yang memakai helper dan skrip sama dengan identitas/nama shortcut/lokasi terpisah: gagal koneksi saat instalasi baru dan pemasangan ulang, unduh rilis publik GitHub, metadata versi Windows, shortcut, perbaikan versi sama, penolakan downgrade dari fixture 0.1.0.3 ke rilis 0.1.0.2, dan uninstall. Sentinel `.mpb` tetap utuh; sentinel tersebut bukan dokumen keuangan valid. Tes ini tidak menggantikan alur Save/Open.

Pengujian wizard pada paket QA final juga menghentikan unduhan melalui **Stop download** dan konfirmasi **Yes**. Setup menampilkan **Download aborted**, kemudian keluar lewat Cancel. Dua assertion tambahan memastikan tidak ada EXE/registrasi terpasang dan sentinel tetap utuh. Helper final juga diuji langsung dengan koneksi gagal: exit code 1 dan pesan kesalahan yang dapat dibaca berhasil dibuat.

Log lokal: `qa/web-installer/integration-results.log`, `cancellation-result.log` dan log setup di folder sama. `qa/web-installer/build-logs/web-installer-final-build.log` mencatat pemeriksaan inti/WPF. Tidak ada publikasi atau perubahan kanal GitHub.

Pada **9 November 2026**, pengguna juga melaporkan pemasangan melalui web installer berhasil. Laporan ini mengonfirmasi pemasangan di komputernya; belum merupakan pengujian seluruh skenario Windows. Paket preview lama beserta checksum disimpan di `dist/archive/installer-preview-2026-11-09/`, terpisah dari web installer aktif.

Pada pengujian awal sebelum rilis 0.1.1.0, kanal publik menunjuk **app-v0.1.0.2**. Rilis [0.1.1.0](releases/0.1.1.0.md) menyediakan paket web installer sebagai aset unduhan dan memasukkan perbaikan startup untuk sinkronisasi versi Windows serta mutex. Installer yang sama mengikuti versi aktif di kanal setelah promosi rilis.

Pemeriksaan mesin Windows bersih, akun standar terpisah, proxy perusahaan, SmartScreen dan interupsi daya belum dilakukan. Installer belum ditandatangani digital. Build installer di CI belum diaktifkan; aset installer tetap yang telah diverifikasi diunggah terpisah ke rilis 0.1.1.0.
