using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;
using System.Windows.Forms;

namespace ETACO.CommonUtils
{
    public class ExcelManager : IDisposable
    {
        private static Type type = Type.GetTypeFromProgID(AppContext.Config["excel", "application", "Excel.Application"]);//"Excel.Application.9"
        private readonly object excel;
        private object _workbook;

        public static void OpenExcel(object content)
        {
            Clipboard.SetData(DataFormats.Text, content);
            var v = new ExcelManager();
            v.excel._SetProperty("Application.Visible", true);
            v.GetWorksheet(1).Eval("Paste(Range('A1'))");
        }

        /// <summary>  Конструктор </summary>
        /// <param name="templateFileName"> имя файла шаблона </param>
        public ExcelManager(string templateFileName = "")
        {
            if (type == null) throw new Exception("COM object Excel.Application not found");
            excel = Activator.CreateInstance(type);
            excel._SetProperty("DisplayAlerts", false);
            excel._SetProperty("IgnoreRemoteRequests", true);
            _workbook = templateFileName.IsEmpty() ? excel._GetProperty("Workbooks")._InvokeMethod("Add") : excel._GetProperty("Workbooks")._InvokeMethod("Open", templateFileName);
        }

        public Worksheet GetWorksheet(int index)
        {
            return new Worksheet(_workbook._GetProperty("Worksheets", index));
        }

        public Worksheet GetWorksheet(string name)
        {
            return new Worksheet(_workbook._GetProperty("Worksheets", name));
        }

        public Worksheet CreateWorksheet()
        {
            return new Worksheet(excel._GetProperty("Worksheets")._InvokeMethod("Add"));
        }

        /// <summary> Сохранение документа </summary>
        /// <param name="defaultWorksheetIndex"> номер страницы по умолчанию</param>
        /// <param name="fileName"> имя файла </param>
        /// <param name="xlFileFormat"> формат формируемого файла (см. XlFileFormat Enumeration)</param>
        public void SaveAs(string fileName, int defaultWorksheetIndex = 1, XlFileFormat xlFileFormat = XlFileFormat.xlNone)
        {
            using (var w = GetWorksheet(defaultWorksheetIndex).Activate()) 
            {
                _workbook._InvokeMethod("SaveAs", fileName, xlFileFormat == XlFileFormat.xlNone ? Type.Missing : xlFileFormat, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, 3/*XlSaveAsAccessMode.xlExclusive*/);
            }
        }

        /// <summary> Сохранение документа </summary>
        public void Save(int defaultWorksheetIndex = 1)
        {
            using (var w = GetWorksheet(defaultWorksheetIndex).Activate())
            {
                _workbook._InvokeMethod("Save");
            }
        }

        public int WorksheetsCount
        {
            get { return Convert.ToInt32(_workbook._GetProperty("Worksheets.Count"));}
        }

        public void RefreshAll()
        {
            _workbook._InvokeMethod("RefreshAll");
        }

        public void CalculateFull()
        {
            excel._InvokeMethod("CalculateFull");
        }

        public object Calculate(string expr)//Calculate("MAX(1,42)")
        {
            return excel._InvokeMethod("Evaluate" , expr);
        }

        public object RunMacro(params object[] args)//!!! refactor to excel._invokeMethod
        {        
            return args.Length == 0? null :  Eval("Application.Run({0})".FormatStr(string.Join(",", Enumerable.Range(0, args.Length).Select(x => "$[" + x + "]"))), args);
        }

        public object Eval(string code, params object[] args)
        {
            return _workbook._Invoke(code, args);
        }

        /// <summary> Работа с объектами Excel (fix bug: Old format or invalid type library)</summary>
        /// <param name="action"> действие с объектами Excel </param>
        public T DoAction<T>(Func<T> action, bool manualCalc = false)
        {
            if (manualCalc) {
                excel._SetProperty("Calculation", XlCalculation.xlCalculationManual);
                excel._SetProperty("ScreenUpdating", false);
            }
            var oldCI = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
                return action();
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = oldCI;
                if (manualCalc) {
                    excel._SetProperty("Calculation", XlCalculation.xlCalculationAutomatic);
                    excel._SetProperty("ScreenUpdating", true);
                }
            }
        }

        public void Dispose()
        {
            _workbook._InvokeMethod("Close");
            Marshal.ReleaseComObject(_workbook);
            excel._SetProperty("IgnoreRemoteRequests", false);
            excel._InvokeMethod("Quit");
            Marshal.ReleaseComObject(excel);
        }

        public class Worksheet : IDisposable
        {
            private object _worksheet;
            //private JSEval _js = null;

            internal Worksheet(object worksheet)
            {
                if (worksheet == null) throw new Exception("Worksheet is null");
                _worksheet = worksheet;
            }

            public int Index { get { return (int)_worksheet._GetProperty("Index"); }}
            public string Name { get { return _worksheet._GetProperty("Name")+""; } }
            /// <summary> Количество строк на странице</summary>
            public int LastRowIndex{ get { return Convert.ToInt32(_worksheet._GetProperty("UsedRange.Rows.Count")); }}
            /// <summary> Количество столбцов на странице</summary>
            public int LastColumnIndex { get { return Convert.ToInt32(_worksheet._GetProperty("UsedRange.Columns.Count")); }}

            public Worksheet Activate() { _worksheet._InvokeMethod("Activate"); return this; }

            public object GetCell(int rowIndex, int columnIndex)
            {
                return _worksheet._GetProperty("Cells", rowIndex, columnIndex);
            }

            /// <summary> Запись значения в ячейку</summary>
            /// <param name="worksheetIndex"> индекс страницы</param>
            /// <param name="rowIndex">индекс строки </param>
            /// <param name="columnIndex">индекс столбца </param>
            /// <param name="value">значение </param>
            public void SetValue(int rowIndex, int columnIndex, object value)
            {
                GetCell(rowIndex, columnIndex)._SetProperty("Value", Type.Missing, value);
            }

            public void SetValue(int rowIndex, int columnIndex, object[] values)
            {
                Eval("Range(Cells($[0], $[1]),Cells($[0], $[2])).Value=$[3]", rowIndex, columnIndex, columnIndex + values.Length - 1, values);
            }

            public void SetValue(int rowIndex, int columnIndex, object[,] values)
            {
                Eval("Range(Cells($[0], $[1]), Cells($[2], $[3])).Value=$[4]", rowIndex, columnIndex, rowIndex + values.GetLongLength(0) - 1, columnIndex + values.GetLongLength(1) - 1, values);
            }

            /// <summary> Запись значения в ячейку</summary>
            /// <param name="rcAddress"> в формуле используется стиль адрессации R[i]C[j] </param>
            public void SetFormula(int rowIndex, int columnIndex, object formula, bool rcAddress = false)
            {
                GetCell(rowIndex, columnIndex)._SetProperty(rcAddress ? "FormulaR1C1" : "Formula", formula);
            }

            /// <summary> Получить значение ячейки </summary>
            /// <param name="worksheetIndex"> индекс страницы</param>
            /// <param name="rowIndex">индекс строки </param>
            /// <param name="columnIndex">индекс столбца </param>
            public object GetValue(int rowIndex, int columnIndex) 
            {
                return GetCell(rowIndex, columnIndex)._GetProperty("Value");
            }

            public object[,] GetValue(int rowIndex, int columnIndex, int width, int height=1)
            {
                return (object[,])Eval("Range(Cells($[0], $[1]),Cells($[2], $[3])).Value", rowIndex, columnIndex, rowIndex + height - 1, columnIndex + width - 1);
            }

            public void RefreshAll()
            {
                Eval("for(var i = 1; i<= PivotTables.Count;i++){var p = PivotTables(i); p.RefreshTable(); p.Update();}");
            }

            public object Eval(string code, params object[] args)
            {
                return _worksheet._Invoke(code, args);
            }

            public void Dispose()
            {
                Marshal.ReleaseComObject(_worksheet);
            }
        }
        
        public enum XlFileFormat
        {
            xlNone = 0,
            xlAddIn = 0x12,
            xlAddIn8 = 0x12,
            xlCSV = 6,
            xlCSVMac = 0x16,
            xlCSVMSDOS = 0x18,
            xlCSVWindows = 0x17,
            xlCurrentPlatformText = -4158,
            xlDBF2 = 7,
            xlDBF3 = 8,
            xlDBF4 = 11,
            xlDIF = 9,
            xlExcel12 = 50,
            xlExcel2 = 0x10,
            xlExcel2FarEast = 0x1b,
            xlExcel3 = 0x1d,
            xlExcel4 = 0x21,
            xlExcel4Workbook = 0x23,
            xlExcel5 = 0x27,
            xlExcel7 = 0x27,
            xlExcel8 = 0x38,
            xlExcel9795 = 0x2b,
            xlHtml = 0x2c,
            xlIntlAddIn = 0x1a,
            xlIntlMacro = 0x19,
            xlOpenDocumentSpreadsheet = 60,
            xlOpenXMLAddIn = 0x37,
            xlOpenXMLTemplate = 0x36,
            xlOpenXMLTemplateMacroEnabled = 0x35,
            xlOpenXMLWorkbook = 0x33,
            xlOpenXMLWorkbookMacroEnabled = 0x34,
            xlSYLK = 2,
            xlTemplate = 0x11,
            xlTemplate8 = 0x11,
            xlTextMac = 0x13,
            xlTextMSDOS = 0x15,
            xlTextPrinter = 0x24,
            xlTextWindows = 20,
            xlUnicodeText = 0x2a,
            xlWebArchive = 0x2d,
            xlWJ2WD1 = 14,
            xlWJ3 = 40,
            xlWJ3FJ3 = 0x29,
            xlWK1 = 5,
            xlWK1ALL = 0x1f,
            xlWK1FMT = 30,
            xlWK3 = 15,
            xlWK3FM3 = 0x20,
            xlWK4 = 0x26,
            xlWKS = 4,
            xlWorkbookDefault = 0x33,
            xlWorkbookNormal = -4143,
            xlWorks2FarEast = 0x1c,
            xlWQ1 = 0x22,
            xlXMLSpreadsheet = 0x2e
        }
        //Sort
        //xlAscending = 1;
        //xlDescending = 2;
        public enum XlCalculation
        {
            xlCalculationManual = -4135,
            xlCalculationAutomatic = -4105,
            xlCalculationSemiautomatic = 2
        }

        public enum XlSheetVisibility
        {
            xlSheetVisible = -1,
            xlSheetHidden = 0,
            xlSheetVeryHidden = 2
        }
    }
}
