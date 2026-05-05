# Spreadtrum (C# Port)

## 📖 Overview
This project is a **full port from C to C#** of the *Spreadtrum Flash* tool, originally developed in [TomKing062/spreadtrum_flash](https://github.com/TomKing062/spreadtrum_flash).  
The main goals of this port are:
- Preserve **1:1 behavioral fidelity** with the original C implementation.  
- Use **pure managed C#** with `byte[]` and `Span<byte>` for buffer management.  
- Maintain the **single-buffer + offset tracking** architecture for easy comparison with the C code.  
- Ensure compatibility with **.NET Framework 4.8** and **Native AOT**.
