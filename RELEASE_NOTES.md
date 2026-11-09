# Piggy 0.1.1.0 — Web Installer and Display Fixes

**Jenis perubahan: Perbaikan Fitur.** Pembaruan Windows x64 dari 0.1.0.2. Format dokumen **9** dan updater epoch **3** dipertahankan; dokumen keuangan yang sudah ada tetap kompatibel.

- **Web installer tersedia:** unduh `Piggy-Piggy-Web-Setup-win-x64.exe` untuk pemasangan per pengguna, shortcut Start Menu/Desktop opsional, dan uninstaller Windows. Internet diperlukan saat pemasangan; pengguna tidak perlu memasang .NET terpisah.
- **Installer tetap mengikuti kanal resmi GitHub:** paket diverifikasi dengan SHA-256 dan versi EXE sebelum pemasangan. Gagal koneksi, paket rusak, dan pembatalan unduhan menghentikan persiapan tanpa menimpa aplikasi yang terpasang. Versi EXE lebih baru menolak downgrade.
- **Perbaikan tampilan DPI:** ukuran minimum dan posisi jendela mengikuti area kerja monitor saat startup, perubahan DPI dan perpindahan monitor. Area kerja yang sempit menyediakan pengguliran agar isi dan status bar tetap terjangkau.
- **Versi Windows diselaraskan:** startup aplikasi terpasang memperbarui nomor versi pada daftar aplikasi Windows setelah update. Pemeriksaan AppId dan lokasi EXE mencegah perubahan registrasi dari aplikasi portable di folder lain.
- **Pemasangan ulang lebih aman:** build aplikasi ini menyediakan mutex yang dikenali installer/uninstaller. Simpan pekerjaan dan tutup Piggy sebelum memasang ulang.

Paket portable tetap tersedia sebagai `Piggy-Piggy-0.1.1.0-win-x64.zip`, termasuk untuk updater dalam aplikasi. Berkas `.sha256` disediakan untuk ZIP dan web installer. Web installer yang diterbitkan merupakan berkas tetap yang sebelumnya telah berhasil diuji pemasangannya oleh pengguna; versi internal installer terpisah dari versi aplikasi yang diunduh.

Validasi mencakup pemeriksaan inti/WPF, respons kanal 0.1.0.2 → 0.1.1.0, checksum dan versi paket, serta pengujian installer terisolasi sebelumnya (koneksi gagal, pemasangan ulang, penolakan downgrade, pembatalan unduhan dan uninstall). Pengujian DPI memakai transisi tersimulasi; perpindahan monitor fisik dengan DPI berbeda dan mesin Windows bersih belum diverifikasi. Installer belum ditandatangani digital.

Pemeriksaan lokal untuk versi 0.1.1.0: **446 pemeriksaan inti dan 45 pemeriksaan WPF lulus**, serta publish EXE self-contained Windows x64 berhasil. Verifikasi paket CI dan kanal publik dicatat pada `docs/VERIFICATION.md`.
