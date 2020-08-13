using ClosedXML.Excel;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;

namespace Sumtotal.ConfigurationsAutomation.Utilities
{
    public class Helper

    {

        public static DataTable ToDataTable<T>(List<T> items)
        {

            DataTable dataTable = new DataTable(typeof(T).Name);

            //Get all the properties

            PropertyInfo[] Props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in Props)

            {

                //Setting column names as Property names

                dataTable.Columns.Add(prop.Name);

            }

            foreach (T item in items)

            {

                var values = new object[Props.Length];

                for (int i = 0; i < Props.Length; i++)

                {

                    //inserting property values to datatable rows

                    values[i] = Props[i].GetValue(item, null);

                }

                dataTable.Rows.Add(values);

            }

            //put a breakpoint here and check datatable

            return dataTable;

        }
        public static DataTable FileContentToDataTable(string strFilePath, bool isHeaderPresent = true)
        {

            DataTable tbl = new DataTable();
            string[] lines = File.ReadAllLines(strFilePath);
            if (lines == null && lines.Length == 0)
            {
                return null;
            }
            int iNoOfColumns = lines[0].TrimEnd(',').Split(',').Length;
            if (isHeaderPresent)
            {
                for (int col = 0; col < iNoOfColumns; col++)
                {
                    var cols = lines[0].Split(',');
                    tbl.Columns.Add(new DataColumn(cols[col].ToString()));
                }
            }
            else
            {
                for (int col = 0; col < iNoOfColumns; col++)
                {
                    tbl.Columns.Add(new DataColumn());
                }
            }
            int startno = isHeaderPresent ? 1 : 0;
            for (int iRowCount = startno; iRowCount < lines.Length; iRowCount++)
            {
                var cols = lines[iRowCount].Split(',');
                DataRow dr = tbl.NewRow();
                for (int cIndex = 0; cIndex < iNoOfColumns; cIndex++)
                {
                    dr[cIndex] = cols[cIndex];
                }
                tbl.Rows.Add(dr);
            }
            return tbl;
        }

        public static XLWorkbook AddWorkSheet(DataTable dt, string sheetName, XLWorkbook workbook)
        {

            var ws = workbook.Worksheets.Add(sheetName);
            int i = 0;
            int j = 0;
            //Header
            for (i = 0; i < dt.Columns.Count; i++)
            {
                ws.Cell(1, i + 1).Value = dt.Columns[i].ColumnName;

            }
            //Data rows

            for (i = 0; i < dt.Rows.Count; i++)
            {
                for (j = 0; j < dt.Columns.Count; j++)
                {
                    ws.Cell(i + 2, j + 1).Value = dt.Rows[i][j];
                }
            }
            return workbook;
        }

    }
}
