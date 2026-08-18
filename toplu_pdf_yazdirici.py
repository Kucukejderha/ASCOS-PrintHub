# ASCOS PrintHub - Toplu PDF yazdırma aracı (eski prototip)
# Copyright (C) 2026 ASCOS
#
# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program.  If not, see <https://www.gnu.org/licenses/>.

import ctypes
import os
import queue
import threading
import time
import tkinter as tk
from pathlib import Path
from tkinter import filedialog, messagebox, ttk


APP_TITLE = "Toplu PDF Yazdırıcı"
SW_HIDE = 0


def list_printers():
    """Return local and connected Windows printer names via Winspool."""
    winspool = ctypes.WinDLL("winspool.drv")
    flags = 0x2 | 0x4  # PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS
    needed = ctypes.c_ulong()
    returned = ctypes.c_ulong()
    winspool.EnumPrintersW(flags, None, 4, None, 0, ctypes.byref(needed), ctypes.byref(returned))
    if not needed.value:
        return []
    buffer = ctypes.create_string_buffer(needed.value)
    if not winspool.EnumPrintersW(
        flags, None, 4, buffer, needed.value, ctypes.byref(needed), ctypes.byref(returned)
    ):
        return []

    class PRINTER_INFO_4(ctypes.Structure):
        _fields_ = [("pPrinterName", ctypes.c_wchar_p), ("pServerName", ctypes.c_wchar_p), ("Attributes", ctypes.c_ulong)]

    items = ctypes.cast(buffer, ctypes.POINTER(PRINTER_INFO_4))
    return sorted({items[i].pPrinterName for i in range(returned.value) if items[i].pPrinterName}, key=str.casefold)


def default_printer():
    winspool = ctypes.WinDLL("winspool.drv")
    size = ctypes.c_ulong()
    winspool.GetDefaultPrinterW(None, ctypes.byref(size))
    if not size.value:
        return ""
    buf = ctypes.create_unicode_buffer(size.value)
    return buf.value if winspool.GetDefaultPrinterW(buf, ctypes.byref(size)) else ""


def send_to_printer(pdf_path, printer_name):
    """Ask the registered PDF application to print to the selected printer."""
    result = ctypes.windll.shell32.ShellExecuteW(
        None, "printto", str(pdf_path), f'"{printer_name}"', str(pdf_path.parent), SW_HIDE
    )
    if result <= 32:
        raise OSError(
            f"Windows yazdırma komutunu başlatamadı (hata {result}). "
            "Bir PDF okuyucunun kurulu ve .pdf dosyalarıyla ilişkilendirilmiş olduğundan emin olun."
        )


class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title(APP_TITLE)
        self.geometry("760x590")
        self.minsize(680, 520)
        self.stop_event = threading.Event()
        self.events = queue.Queue()
        self.worker = None

        self.folder_var = tk.StringVar()
        self.printer_var = tk.StringVar()
        self.recursive_var = tk.BooleanVar(value=False)
        self.copies_var = tk.IntVar(value=1)
        self.delay_var = tk.DoubleVar(value=2.0)
        self.status_var = tk.StringVar(value="Hazır")
        self._build_ui()
        self.refresh_printers()
        self.after(100, self.process_events)

    def _build_ui(self):
        root = ttk.Frame(self, padding=16)
        root.pack(fill="both", expand=True)
        root.columnconfigure(1, weight=1)
        root.rowconfigure(6, weight=1)

        ttk.Label(root, text="PDF klasörü:").grid(row=0, column=0, sticky="w", pady=6)
        ttk.Entry(root, textvariable=self.folder_var).grid(row=0, column=1, sticky="ew", padx=8)
        ttk.Button(root, text="Klasör Seç…", command=self.choose_folder).grid(row=0, column=2)

        ttk.Label(root, text="Yazıcı:").grid(row=1, column=0, sticky="w", pady=6)
        self.printer_box = ttk.Combobox(root, textvariable=self.printer_var, state="readonly")
        self.printer_box.grid(row=1, column=1, sticky="ew", padx=8)
        ttk.Button(root, text="Yenile", command=self.refresh_printers).grid(row=1, column=2, sticky="ew")

        options = ttk.Frame(root)
        options.grid(row=2, column=0, columnspan=3, sticky="ew", pady=(8, 4))
        ttk.Checkbutton(options, text="Alt klasörlerdeki PDF'leri de dahil et", variable=self.recursive_var).pack(side="left")
        ttk.Label(options, text="Kopya:").pack(side="left", padx=(24, 5))
        ttk.Spinbox(options, from_=1, to=99, width=5, textvariable=self.copies_var).pack(side="left")
        ttk.Label(options, text="Dosyalar arası bekleme (sn):").pack(side="left", padx=(24, 5))
        ttk.Spinbox(options, from_=0, to=60, increment=0.5, width=6, textvariable=self.delay_var).pack(side="left")

        buttons = ttk.Frame(root)
        buttons.grid(row=3, column=0, columnspan=3, sticky="ew", pady=10)
        self.scan_button = ttk.Button(buttons, text="PDF'leri Listele", command=self.scan)
        self.scan_button.pack(side="left")
        self.print_button = ttk.Button(buttons, text="Yazdırmayı Başlat", command=self.start_printing)
        self.print_button.pack(side="left", padx=8)
        self.stop_button = ttk.Button(buttons, text="Durdur", command=self.stop_printing, state="disabled")
        self.stop_button.pack(side="left")

        self.progress = ttk.Progressbar(root, mode="determinate")
        self.progress.grid(row=4, column=0, columnspan=3, sticky="ew", pady=(0, 4))
        ttk.Label(root, textvariable=self.status_var).grid(row=5, column=0, columnspan=3, sticky="w")

        self.log = tk.Text(root, height=18, wrap="none", state="disabled")
        self.log.grid(row=6, column=0, columnspan=3, sticky="nsew", pady=(8, 0))
        scrollbar = ttk.Scrollbar(root, orient="vertical", command=self.log.yview)
        scrollbar.grid(row=6, column=3, sticky="ns", pady=(8, 0))
        self.log.configure(yscrollcommand=scrollbar.set)

    def choose_folder(self):
        folder = filedialog.askdirectory(title="PDF klasörünü seçin")
        if folder:
            self.folder_var.set(folder)
            self.scan()

    def refresh_printers(self):
        try:
            printers = list_printers()
            self.printer_box["values"] = printers
            current_default = default_printer()
            if current_default in printers:
                self.printer_var.set(current_default)
            elif printers and self.printer_var.get() not in printers:
                self.printer_var.set(printers[0])
        except Exception as exc:
            messagebox.showerror(APP_TITLE, f"Yazıcılar alınamadı:\n{exc}")

    def pdf_files(self):
        folder = Path(self.folder_var.get().strip())
        if not folder.is_dir():
            raise ValueError("Geçerli bir PDF klasörü seçin.")
        pattern = "**/*.pdf" if self.recursive_var.get() else "*.pdf"
        return sorted(folder.glob(pattern), key=lambda p: str(p.relative_to(folder)).casefold())

    def scan(self):
        try:
            files = self.pdf_files()
        except ValueError as exc:
            messagebox.showwarning(APP_TITLE, str(exc))
            return
        self.clear_log()
        for index, path in enumerate(files, 1):
            self.append_log(f"{index:03d}  {path}")
        self.status_var.set(f"{len(files)} PDF bulundu.")

    def start_printing(self):
        if self.worker and self.worker.is_alive():
            return
        try:
            files = self.pdf_files()
            copies = int(self.copies_var.get())
            delay = float(self.delay_var.get())
            printer = self.printer_var.get().strip()
            if not files:
                raise ValueError("Seçilen klasörde PDF bulunamadı.")
            if not printer:
                raise ValueError("Bir yazıcı seçin.")
            if copies < 1 or delay < 0:
                raise ValueError("Kopya ve bekleme değerlerini kontrol edin.")
        except (ValueError, tk.TclError) as exc:
            messagebox.showwarning(APP_TITLE, str(exc))
            return

        if not messagebox.askyesno(APP_TITLE, f"{len(files)} PDF, {copies} kopya olarak\n'{printer}' yazıcısına gönderilsin mi?"):
            return
        self.stop_event.clear()
        self.progress.configure(maximum=len(files) * copies, value=0)
        self.set_running(True)
        self.worker = threading.Thread(target=self.print_worker, args=(files, printer, copies, delay), daemon=True)
        self.worker.start()

    def print_worker(self, files, printer, copies, delay):
        total = len(files) * copies
        completed = 0
        try:
            for path in files:
                for copy_no in range(1, copies + 1):
                    if self.stop_event.is_set():
                        self.events.put(("done", False, "Yazdırma kullanıcı tarafından durduruldu."))
                        return
                    send_to_printer(path, printer)
                    completed += 1
                    suffix = f" (kopya {copy_no}/{copies})" if copies > 1 else ""
                    self.events.put(("progress", completed, total, f"Gönderildi: {path.name}{suffix}"))
                    if completed < total and self.stop_event.wait(delay):
                        self.events.put(("done", False, "Yazdırma kullanıcı tarafından durduruldu."))
                        return
            self.events.put(("done", True, f"Tamamlandı: {completed} yazdırma işi kuyruğa gönderildi."))
        except Exception as exc:
            self.events.put(("error", str(exc)))

    def stop_printing(self):
        self.stop_event.set()
        self.status_var.set("Durduruluyor…")

    def process_events(self):
        try:
            while True:
                event = self.events.get_nowait()
                if event[0] == "progress":
                    _, completed, total, message = event
                    self.progress["value"] = completed
                    self.status_var.set(f"{completed}/{total} — {message}")
                    self.append_log(message)
                elif event[0] == "done":
                    _, success, message = event
                    self.set_running(False)
                    self.status_var.set(message)
                    self.append_log(message)
                    (messagebox.showinfo if success else messagebox.showwarning)(APP_TITLE, message)
                elif event[0] == "error":
                    self.set_running(False)
                    self.status_var.set("Hata oluştu.")
                    self.append_log(f"HATA: {event[1]}")
                    messagebox.showerror(APP_TITLE, event[1])
        except queue.Empty:
            pass
        self.after(100, self.process_events)

    def set_running(self, running):
        state = "disabled" if running else "normal"
        self.print_button.configure(state=state)
        self.scan_button.configure(state=state)
        self.stop_button.configure(state="normal" if running else "disabled")

    def append_log(self, text):
        self.log.configure(state="normal")
        self.log.insert("end", text + "\n")
        self.log.see("end")
        self.log.configure(state="disabled")

    def clear_log(self):
        self.log.configure(state="normal")
        self.log.delete("1.0", "end")
        self.log.configure(state="disabled")


if __name__ == "__main__":
    if os.name != "nt":
        raise SystemExit("Bu uygulama yalnızca Windows üzerinde çalışır.")
    App().mainloop()
