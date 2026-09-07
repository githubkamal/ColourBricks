export interface ExportTable {
  /** File name without extension. */
  filename: string;
  title: string;
  columns: string[];
  rows: (string | number)[][];
}

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

export function exportTableCsv(table: ExportTable): void {
  const header = table.columns.map((c) => JSON.stringify(c)).join(",");
  const body = table.rows
    .map((row) => row.map((v) => JSON.stringify(v ?? "")).join(","))
    .join("\n");
  const blob = new Blob([`${table.title}\n${header}\n${body}`], { type: "text/csv" });
  downloadBlob(blob, `${table.filename}.csv`);
}

export async function exportTableExcel(table: ExportTable): Promise<void> {
  const ExcelJS = (await import("exceljs")).default;
  const workbook = new ExcelJS.Workbook();
  const sheet = workbook.addWorksheet((table.title || "Sheet1").slice(0, 31));
  sheet.addRow(table.columns);
  sheet.getRow(1).font = { bold: true };
  for (const row of table.rows) sheet.addRow(row);
  sheet.columns.forEach((col) => {
    col.width = 20;
  });
  const buffer = await workbook.xlsx.writeBuffer();
  downloadBlob(
    new Blob([buffer], {
      type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    }),
    `${table.filename}.xlsx`,
  );
}

export async function exportTablePdf(table: ExportTable): Promise<void> {
  const { jsPDF } = await import("jspdf");
  const { autoTable } = await import("jspdf-autotable");
  const doc = new jsPDF({ orientation: table.columns.length > 6 ? "landscape" : "portrait" });
  doc.setFontSize(13);
  doc.text(table.title, 14, 15);
  autoTable(doc, {
    startY: 20,
    head: [table.columns],
    body: table.rows.map((row) => row.map((v) => String(v ?? ""))),
    styles: { fontSize: 8 },
    headStyles: { fillColor: [30, 41, 59] },
  });
  doc.save(`${table.filename}.pdf`);
}
