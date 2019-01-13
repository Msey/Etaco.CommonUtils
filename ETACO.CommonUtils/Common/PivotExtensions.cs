using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ETACO.CommonUtils
{
    static class PivotExtensions
    {
        public static DataTable Pivot(this DataTable dt, DataColumn keyColumn, DataColumn valColumn, Func<object,object,object> aggr = null)
        {
            var temp = dt.Copy();
            temp.Columns.Remove(keyColumn.ColumnName);
            temp.Columns.Remove(valColumn.ColumnName);
            var pkColumnNames = temp.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();

            var result = temp.DefaultView.ToTable(true, pkColumnNames).Copy();
            result.PrimaryKey = result.Columns.Cast<DataColumn>().ToArray();
            dt.Select().Select(r => r[keyColumn.ColumnName].ToString()).Distinct().ToList().ForEach(c => result.Columns.Add(c, valColumn.DataType));

            foreach (DataRow row in dt.Rows)
            {
                var aggRow = result.Rows.Find(pkColumnNames.Select(c => row[c]).ToArray());
                var col = row[keyColumn.ColumnName].ToString();
                aggRow[col] = aggr==null? row[valColumn.ColumnName]:aggr(row[valColumn.ColumnName], aggRow[col]);
            }
            return result;
        }
        /*var l = new System.Data.DataTable();      //[Name,Department,Function,Salay]      => [Name,Function,R&D,Dev]
        l.Columns.Add("Name");                      //["Mike", "Dev", "Consultant", 5000]   => ["Mike","Consultant",0,20000]
        var key = l.Columns.Add("Department");
        l.Columns.Add("Function");
        var val = l.Columns.Add("Salary", typeof(decimal));
        l.Rows.Add("Fons", "R&D", "Trainer", 2000);
        l.Rows.Add("Jim", "R&D", "Trainer", 3000);
        l.Rows.Add("Ellen", "Dev", "Developer", 4000);
        l.Rows.Add("Mike", "Dev", "Consultant", 5000);
        l.Rows.Add("Mike", "Dev", "Consultant", 15000);
        l.Rows.Add("Jack", "R&D", "Developer", 6000);    
        var l2 = l.Pivot(key, val, (x,y)=>Convert.ToDecimal(x)+ Convert.ToDecimal(y is DBNull?0:y));*/

        public static Dictionary<TKey1, Dictionary<TKey2, TValue>> Pivot<TSource, TKey1, TKey2, TValue>(this IEnumerable<TSource> source, Func<TSource, TKey1> key1Selector, Func<TSource, TKey2> key2Selector, Func<IEnumerable<TSource>, TValue> aggregate)
        {
            return source.GroupBy(key1Selector).Select(x => new {
               X = x.Key,
               Y = source.GroupBy(key2Selector).Select(z => new {
                    Z = z.Key,
                    V = aggregate(from item in source where key1Selector(item).Equals(x.Key) && key2Selector(item).Equals(z.Key) select item)
               }).ToDictionary(e => e.Z, o => o.V)
            }).ToDictionary(e => e.X, o => o.Y);
        }
        /*
        internal class Employee
        {
            public string Name { get; set; }
            public string Department { get; set; }
            public string Function { get; set; }
            public decimal Salary { get; set; }
        }
        var l = new List<Employee>() {
            new Employee() { Name = "Fons", Department = "R&D", Function = "Trainer", Salary = 2000 },
            new Employee() { Name = "Jim", Department = "R&D", Function = "Trainer", Salary = 3000 },
            new Employee() { Name = "Ellen", Department = "Dev", Function = "Developer", Salary = 4000 },
            new Employee() { Name = "Mike", Department = "Dev", Function = "Consultant", Salary = 5000 },
            new Employee() { Name = "Jack", Department = "R&D", Function = "Developer", Salary = 6000 }};

        var result5 = l.Pivot(emp => emp.Department, emp2 => emp2.Function, lst => lst.Sum(emp => emp.Salary));
        var result6 = l.Pivot(emp => emp.Function, emp2 => emp2.Department, lst => lst.Count());*/
        /*
        String json = JsonConvert.SerializeObject(pivotArray, new KeyValuePairConverter()); //удобно сериализовать массив
        */
    }
}