# Panduan folder proyek

Folder ini berisi sumber aplikasi dan hasil pengembangan lokal. Untuk pemasangan, gunakan `dist/installer/Piggy-Piggy-Web-Setup-win-x64.exe`. Versi portable hasil build lokal tersedia melalui `dist/Piggy - Piggy/Piggy - Piggy.exe`.

| Lokasi | Fungsi | Perlakuan |
| --- | --- | --- |
| Berkas `.cs`, `.xaml`, `.csproj`, `app.manifest` di root | Kode dan konfigurasi aplikasi | Pertahankan |
| `Core/` | Model, perhitungan, penyimpanan dokumen serta komponen update/paket yang dipakai bersama | Pertahankan |
| `Assets/` | Ikon aplikasi dan lisensinya | Pertahankan |
| `Tests/` | Pemeriksaan inti dan WPF | Pertahankan |
| `installer/Piggy.iss`, `installer/Downloader/` | Skrip web installer dan sumber helper verifikasi | Pertahankan |
| `tools/` | Skrip pendukung pengembangan, misalnya pembuat ikon | Pertahankan |
| `.git/` | Riwayat kode dan koneksi repository | Pertahankan |
| `.github/` | Otomasi pemeriksaan dan build GitHub | Pertahankan |
| `docs/README.md` | Indeks panduan, hasil verifikasi dan laporan historis | Titik awal dokumentasi |
| `docs/reviews/`, `docs/releases/` | Laporan pemeriksaan historis dan catatan rilis | Pertahankan; bukan status kode terbaru |
| `README.md`, `RELEASE_NOTES.md` | Penggunaan aplikasi dan catatan perubahan | Pertahankan |
| `publish.ps1`, `build-installer.ps1` | Build portable dan web installer | Pertahankan |
| `update-channel.json`, `.gitignore`, `.gitattributes` | Kanal update, batas berkas Git dan aturan akhir baris | Pertahankan |
| `dist/installer/` | Web installer aktif beserta SHA-256 | Pertahankan untuk pemasangan/distribusi |
| `dist/Piggy - Piggy/` | Aplikasi portable hasil build terbaru | Pertahankan jika digunakan |
| ZIP versi aktif dan `.sha256` di `dist/` | Paket distribusi dan checksum | Pertahankan untuk distribusi/verifikasi |
| `dist/archive/` | Paket preview lama dan checksum yang dipertahankan | Arsip; gunakan installer aktif untuk pemasangan |
| `bin/`, `obj/` pada setiap proyek, termasuk `installer/Downloader/` | Hasil kompilasi dan cache sementara | Bisa dibuat ulang; tutup aplikasi sebelum membersihkan |
| `obj/installer-helper/win-x64/` | Helper sementara untuk pengemasan installer | Hasil build, bukan paket distribusi |
| `qa/web-installer/` | Skrip, log dan sampel pengujian web installer | Bukti lokal; tinjau sebelum membersihkan |
| `qa/archive/` | Bahan pengujian preview lama, termasuk sampel `.mpb` | Arsip; bukan cache yang boleh dibuang menyeluruh |
| `qa/archive/build-cache-2026-11-09/` | Cache build yang dipindahkan saat penataan repo | Bisa dipulihkan; pengarsipan tidak mengurangi pemakaian disk |
| `qa/repo-cleanup/` | Log verifikasi dan manifest penataan repo lokal | Bukti pembersihan dan pemindahan |
| `design-qa.md` | Catatan pemeriksaan tampilan lokal | Pertahankan sebagai referensi |
| Semua berkas `.mpb` | Dokumen keuangan atau sampel | Pertahankan; bukan cache build |

`bin/`, `obj/`, `dist/`, `qa/`, `design-qa.md` dan dokumen `.mpb` diabaikan Git. Status Git yang bersih tidak berarti berkas tersebut tidak penting atau sudah dicadangkan.

Setelah cache dibersihkan, build berikutnya akan membuat `bin/` dan `obj/` kembali. Pembersihan folder tidak mengubah versi aplikasi atau format dokumen.
