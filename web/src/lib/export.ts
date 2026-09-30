import * as XLSX from "xlsx";
import jsPDF from "jspdf";
import autoTable from "jspdf-autotable";

/**
 * Export helpers used by every report card (Sales, Stock, Waste). Both functions
 * only ever serialize data this app already fetched and rendered — never a
 * user-supplied file — so the known xlsx (SheetJS) parsing CVEs (prototype
 * pollution / ReDoS, both in the *read* path) don't apply here: XLSX.read/
 * XLSX.readFile are never called anywhere in this codebase.
 */

export function exportToExcel(filename: string, sheetName: string, rows: Record<string, unknown>[]) {
  const worksheet = XLSX.utils.json_to_sheet(rows);
  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, worksheet, sheetName);
  XLSX.writeFile(workbook, `${filename}.xlsx`);
}

export function exportToCsv(filename: string, rows: Record<string, unknown>[]) {
  const worksheet = XLSX.utils.json_to_sheet(rows);
  const csv = XLSX.utils.sheet_to_csv(worksheet);
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `${filename}.csv`;
  link.click();
  URL.revokeObjectURL(url);
}

export function exportToPdf(filename: string, title: string, columns: string[], rows: (string | number)[][]) {
  const doc = new jsPDF();
  doc.setFontSize(14);
  doc.text(title, 14, 15);
  autoTable(doc, {
    head: [columns],
    body: rows,
    startY: 20,
    styles: { fontSize: 9 },
    headStyles: { fillColor: [60, 52, 137] },
  });
  doc.save(`${filename}.pdf`);
}
