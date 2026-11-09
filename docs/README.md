# Dokumentasi Piggy

| Panduan | Isi |
| --- | --- |
| [Penggunaan aplikasi](../README.md) | Fitur, dokumen keuangan dan cara menjalankan |
| [Struktur folder](FOLDER_GUIDE.md) | Sumber kode, hasil build, distribusi dan arsip QA |
| [Web installer](INSTALLER.md) | Cara build, perilaku pemasangan, hasil uji dan batas rollout |
| [Rilis dan kanal update](RELEASING.md) | Penomoran versi, paket GitHub dan proses publikasi |
| [Verifikasi](VERIFICATION.md) | Catatan pemeriksaan aplikasi dari tiap tahap pengembangan |
| [Identitas visual](BRANDING.md) | Logo, ikon dan panduan aset |
| [Catatan perubahan](../RELEASE_NOTES.md) | Perubahan pada versi aplikasi |
| [Baseline 0.1.0.0](releases/0.1.0.0.md) | Catatan rilis awal |
| [Rilis 0.1.1.0](releases/0.1.1.0.md) | Web installer, perbaikan DPI dan versi Windows |
| [Rilis 0.1.1.1](releases/0.1.1.1.md) | Ikon grayscale MPB dan perbaikan kepemilikan asosiasi |
| [Review preview installer](reviews/2026-11-09-installer-preview.md) | Laporan historis sebelum beralih ke web installer |
| [Review ikon MPB 0.1.1.1](reviews/2026-11-09-mpb-icon-0.1.1.1.md) | Riwayat bug cleanup default dan perbaikannya |

Paket distribusi, dokumen `.mpb`, cache build dan bukti QA lokal tidak disimpan di Git. Nomor versi di sumber lokal tidak otomatis berarti perubahan tersebut sudah diterbitkan; rilis publik yang diikuti aplikasi ditentukan oleh `update-channel.json` di GitHub.
