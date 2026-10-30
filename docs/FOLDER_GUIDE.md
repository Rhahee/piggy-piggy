# Panduan folder proyek

Folder ini berisi sumber aplikasi sekaligus hasil pengembangan. Untuk memakai aplikasi, buka `dist/Piggy - Piggy/Piggy - Piggy.exe`.

| Lokasi | Fungsi | Perlakuan |
| --- | --- | --- |
| Berkas `.cs`, `.xaml`, `.csproj`, `app.manifest` di root | Kode dan konfigurasi aplikasi | Pertahankan |
| `Core/` | Model, perhitungan, validasi dan penyimpanan dokumen | Pertahankan |
| `Assets/` | Ikon aplikasi dan lisensinya | Pertahankan |
| `Tests/` | Pemeriksaan inti dan WPF | Pertahankan |
| `.git/` | Riwayat kode dan koneksi repository | Pertahankan |
| `.github/` | Otomasi pemeriksaan dan build GitHub | Pertahankan |
| `docs/`, `README.md`, `RELEASE_NOTES.md` | Panduan dan catatan rilis | Pertahankan |
| `publish.ps1`, `update-channel.json`, `.gitignore` | Build, kanal update dan aturan Git | Pertahankan |
| `dist/Piggy - Piggy/` | Aplikasi portable hasil build terbaru | Pertahankan jika digunakan |
| ZIP versi aktif dan `.sha256` di `dist/` | Paket distribusi dan checksum | Pertahankan untuk distribusi/verifikasi |
| Build dan ZIP versi lama di `dist/` | Hasil build sebelumnya | Boleh dihapus setelah memastikan bukan versi yang sedang dipakai dan tidak menyimpan dokumen pribadi |
| `bin/`, `obj/` di root, `Core/`, `Tests/`, `Tests/Wpf/` | Hasil kompilasi dan cache sementara | Bisa dibuat ulang oleh build; tutup aplikasi sebelum membersihkan |
| `qa/` | Sampel dokumen, skrip, gambar dan bukti pengujian lokal | Tinjau satu per satu; tidak semuanya cache |
| `design-qa.md` | Catatan pemeriksaan tampilan lokal | Pertahankan sebagai referensi |
| Semua berkas `.mpb` | Dokumen keuangan atau sampel | Pertahankan; bukan cache build |

`bin/`, `obj/`, `dist/`, `qa/`, `design-qa.md` dan dokumen `.mpb` diabaikan Git. Status Git yang bersih tidak berarti berkas tersebut tidak penting atau sudah dicadangkan.

Setelah cache dibersihkan, build berikutnya akan membuat `bin/` dan `obj/` kembali. Pembersihan folder tidak mengubah versi aplikasi atau format dokumen.
