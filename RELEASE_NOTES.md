# Unreleased — Transaction layout and scrolling

**Jenis perubahan: Minor Fix.** Kandidat versi berikutnya: **0.1.1.2**; belum dirilis. Versi aplikasi dan kanal publik masih **0.1.1.1**.

- Tanggal transaksi tetap terlihat pada kolom pertama, termasuk saat tabel digeser horizontal.
- Lebar kolom mengikuti ruang jendela, dengan batas minimum untuk bank, kategori, deskripsi dan nominal.
- Ribbon, tab halaman dan status tetap di tempat; hanya isi halaman/tabel yang bergulir.
- Scroll wheel bekerja pada tabel, halaman Overview dan daftar bank yang panjang. Pada batas tabel Overview, scroll diteruskan ke halaman.
- Perbaikan layout tabel ketika skala layar berpindah hingga 200% dan kembali ke 100%.

Validasi lokal: **448 pemeriksaan inti dan 49 WPF lulus**, serta pemeriksaan langsung scroll wheel dan maximize memakai 125 transaksi fiktif. Bukti dan batas pengujian dicatat di `docs/VERIFICATION.md`.

# Piggy 0.1.1.1 — Grayscale Document Icon

**Jenis perubahan: Minor Fix.** Format dokumen **9** dan updater epoch **3** dipertahankan.

- Ikon dokumen `.mpb` memakai koin yang sama dengan ikon aplikasi dalam **grayscale**, berlatar transparan dan tersedia dalam sembilan ukuran ICO dari 16 hingga 256 piksel. Ikon aplikasi tetap emas.
- **Web installer revisi 1.0.0.1** menyediakan pilihan registrasi `.mpb`, ikon dokumen, perintah Open dan Open With per pengguna. Pilihan aplikasi pembuka yang sudah ada dipertahankan.
- **Bug cleanup default diperbaiki:** uninstaller kini hanya membersihkan default `.mpb` yang benar-benar dimiliki instalasi tersebut. Mematikan opsi registrasi tidak lagi menyebabkan default lama ke Piggy terhapus saat uninstall.
- Penanda kepemilikan terikat ke folder instalasi dan dipertahankan saat pemasangan ulang dengan opsi registrasi dimatikan. Cleanup yang sah tetap berjalan, sementara pilihan pembuka yang berubah setelah pemasangan tetap aman.

**Untuk memperoleh ikon/asosiasi `.mpb`, jalankan installer terbaru sekali.** Updater dalam aplikasi hanya mengganti EXE; ia tidak dapat menambah aturan registrasi dan uninstall pada installer lama. Jika Windows masih menyimpan pilihan pembuka lain, pilih Piggy melalui **Open With**. Dokumen keuangan tidak diubah.

Aset rilis: `Piggy-Piggy-0.1.1.1-win-x64.zip`, `Piggy-Piggy-Web-Setup-win-x64.exe`, dan checksum SHA-256 untuk masing-masing paket. Installer lama di rilis 0.1.1.0 tidak diganti.

Uji regresi gagal pada kode lama dan **37 pemeriksaan asosiasi berkas lulus setelah perbaikan**, termasuk pencarian ikon/perintah melalui Windows Shell, default lama + opt-out, pemasangan ulang, default aplikasi lain dan pelestarian sentinel `.mpb`. Bukti pemeriksaan inti/WPF, CI dan paket publik dicatat pada `docs/VERIFICATION.md`. Pengujian memakai ekstensi/ProgID QA terpisah; Windows bersih dan pemilihan UserChoice belum diverifikasi. Installer belum ditandatangani digital.

Build lokal juga lulus **448 pemeriksaan inti dan 45 WPF**, serta menghasilkan EXE portable self-contained dan installer revisi 1.0.0.1.
