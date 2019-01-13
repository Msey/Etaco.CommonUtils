using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ETACO.CommonUtils
{
    /// <summary> Класс привязки табличных коллекций к DataSource элементов отображения данных</summary>>
    public class TableList<T> : ITypedList, IList, IEnumerable<object[]>
    {
        public readonly static TableList<object> Empty = new TableList<object>();
        public readonly List<TLColumnInfo> Columns = new List<TLColumnInfo>();
        public readonly List<TLColumnInfo> ExpandColumns = null;
        public readonly List<object[]> Data = new List<object[]>();
        public readonly Dictionary<int, TLRowState> Changes = new Dictionary<int, TLRowState>();//запись остаётся в таблице, но подсвечивается другим цветом
        private Dictionary<T, int> rowIndex = new Dictionary<T, int>();
        private Dictionary<string, int> columnsIndex = null;
        private TableList() { }//for empty
        /// <remarks>use { Capacity = rowCount} for best performance</remarks>
        public TableList(string c, params string[] cols) : this(new List<TLColumnInfo>() { new TLColumnInfo(c, typeof(T), true) }.Concat(cols.Select(v => new TLColumnInfo(v)))) { }
        public TableList(IEnumerable<TLColumnInfo> ci)
        {
            var i = 0;
            foreach (var v in ci) { v.Index = i++; Columns.Add(v); }
            if (typeof(object) != typeof(T)) Columns[0].DataType = typeof(T);
            Columns[0].ReadOnly = true;
            columnsIndex = Columns.ToDictionary(x => x.Name, x => x.Index);
            ExpandColumns = Columns.SelectMany(x => x.GetColumnInfo()).ToList();//ToDictionary(x=>x.Name, x=>x);
        }
        internal object[] Load(T key) { var v = new object[Columns.Count]; v[0] = key; Data.Add(v); return v; }//use only for load from db
        public virtual int Add(object[] row) { Data.Add(row); var i = Count - 1; SetRowState(i, TLRowState.Added); return i; }//for IList
        public int FindIndex(T key) { return rowIndex.GetValue(key, -1); }
        internal int FindIndex(object key) { return Data.Count == rowIndex.Count ? FindIndex((T)key) : Data.FindIndex(r => ((T)key).Equals((T)r[0])); }
        public void AcceptChanges(bool useIndex = false) { foreach (var i in GetRows(TLRowState.Deleted)) Data.RemoveAt(i); ClearChanges(); rowIndex = new Dictionary<T, int>(Count); if (useIndex) for (var i = 0; i < Count; i++) rowIndex.Add((T)Data[i][0], i); } //unboxing (long)((object)int) => castexception и долгая операция (T)Convert.ChangeType(Data[i][0], t), i);
        //TableList
        public int GetColumnIndex(string name) { return columnsIndex.GetValue(name, -1); }
        public int Count { get { return Data.Count; } }
        public int Capacity { get { return Data.Capacity; } set { Data.Capacity = value; } }
        public bool ColumnsExpanding { get; set; }
        public IReadOnlyList<object> this[int indx] { get { return Data[indx]; } }//fast for read and show
        /// <remarks>direct access</remarks>
        public object this[int indx, int col] { get { return Data[indx][col]; } set { if (col > 0) { Data[indx][col] = value; SetRowState(indx, TLRowState.Changed); }}}
        /// <remarks>expand access</remarks>
        public object this[int indx, TLColumnInfo col] { get { return col?.GetRowValue(Data[indx]); } set { if (col.Index > 0) { col?.SetRowValue(Data[indx], value); if (col.DrillDown.Count == 0) SetRowState(indx, TLRowState.Changed); } } }
        public int IndexOf(object[] row) { return Data.IndexOf(row); }
        //RemoveRange!!! performance
        public void Remove(int indx) { SetRowState(indx, TLRowState.Deleted); }//only for edit in grid
        public void Clear() { Data.Clear(); ClearChanges(); rowIndex.Clear(); }
        public void ClearChanges() { Changes.Clear(); }
        public bool HasChanges() { return Changes.Count > 0; }
        public TLRowState GetRowState(int indx) { return Changes.GetValue(indx, TLRowState.Original); }
        public void SetRowState(int indx, TLRowState state) { if (state == TLRowState.Original) Changes.Remove(indx); else Changes[indx] = state; }
        public int[] GetRows(TLRowState state) { return state == TLRowState.Original ? Data.Select((d, i) => new { r = d, i = i }).Where(x => GetRowState(x.i) == TLRowState.Original).Select(x => x.i).ToArray() : Changes.Where(s => s.Value == state).Select(s => s.Key).ToArray(); }
        //IList
        public bool IsReadOnly { get { return false; } }//use readonly for column
        public bool IsFixedSize { get { return true; } }
        public object SyncRoot { get { return this; } }
        public bool IsSynchronized { get { return false; } }
        object IList.this[int row] { get { return Data[row]; } set { } }
        int IList.Add(object value) { return -1; }//IsFixedSize=true
        bool IList.Contains(object value) { return false; }
        int IList.IndexOf(object value) { return Data.IndexOf((object[])value); }
        void IList.Insert(int index, object value) { }
        void IList.Remove(object value) { }
        void IList.RemoveAt(int index) { }
        public void CopyTo(Array array, int index) { }
        public IEnumerator GetEnumerator() { return Data.GetEnumerator(); }
        IEnumerator<object[]> IEnumerable<object[]>.GetEnumerator() { return Data.GetEnumerator(); }//для LinqServerModeSource дергается вместо Count + IList.this[int row]
        //ITypedList
        public string GetListName(PropertyDescriptor[] listAccessors) { throw new NotImplementedException(); }
        public PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[] listAccessors) { return new PropertyDescriptorCollection((ColumnsExpanding ? ExpandColumns : Columns).Select(c => new _PropertyDescriptor(c)).ToArray()); }
        private class _PropertyDescriptor : PropertyDescriptor
        {
            private TLColumnInfo ci = null;
            public _PropertyDescriptor(TLColumnInfo ci) : base(ci.Name, null) { this.ci = ci; }
            public override bool CanResetValue(object component) { return false; }
            public override void ResetValue(object component) { }
            public override Type ComponentType { get { return typeof(IList); } }
            public override Type PropertyType { get { return ci.DataType; } }
            public override object GetValue(object component) { return ci.GetRowValue(component); }//для pivot дёргается уже после того как колонку бросили на грид (до этого только item+count)
            public override void SetValue(object component, object value) { ci.SetRowValue(component, value); }
            public override bool IsReadOnly { get { return ci.ReadOnly; } }
            public override bool ShouldSerializeValue(object component) { return false; }
        }
    }

    public class TLColumnInfo
    {
        internal List<TLColumnInfo> DrillDown = new List<TLColumnInfo>();
        public string Name { get; internal set; }
        public Type DataType { get; internal set; }//для TableList<object>
        public bool ReadOnly { get; internal set; }
        public object Tag { get; protected set; }
        public int Index { get; internal set; }
        public TLColumnInfo(string name, Type type = null, bool readOnly = false, object tag = null) { Name = name; DataType = type ?? typeof(object); ReadOnly = readOnly; Tag = tag;}
        public virtual List<TLColumnInfo> GetColumnInfo(List<object> tableStack = null) { return new List<TLColumnInfo> { new TLColumnInfo(Name, DataType, ReadOnly, Tag) { Index = Index } }; }

        public object GetRowValue(object row)
        {   //Count > 0 - for perf
            if (DrillDown.Count > 0) foreach(var v in DrillDown) { row = v.FindInDrillDown(row); if (row == null) return null; }
            return ((object[])row)[Index];
        }
        public void SetRowValue(object row, object val)
        {   //Count > 0 - for perf
            if (DrillDown.Count > 0) foreach (var v in DrillDown) { row = v.FindInDrillDown(row); if (row == null) return; }
            ((object[])row)[Index] = val;//??? changes??
        }

        protected virtual object FindInDrillDown(object key) { return null; }
    }

    public class TLColumnInfo<T> : TLColumnInfo
    {
        public TableList<T> LookupTable { get; set; }//public, чтобы можно было сослаться на себя (иерархическая таблица)
        public TLColumnInfo(string name, TableList<T> lookupTable, bool readOnly = false, object tag = null) : base(name, typeof(T), readOnly, tag) { LookupTable = lookupTable; }
        public override List<TLColumnInfo> GetColumnInfo(List<object> tableStack = null)
        {
            var v = new List<TLColumnInfo>() { new TLColumnInfo<T>(Name, LookupTable, ReadOnly, Tag) { Index = Index } };
            if (LookupTable!= null && !(tableStack?.Contains(LookupTable)??false))
            {
                tableStack = tableStack ?? new List<object>();
                tableStack.Add(LookupTable);
                foreach (var c in LookupTable.Columns) foreach (var x in c.GetColumnInfo(tableStack))
                    {
                        v.Add(x);
                        foreach (var dd in x.DrillDown) dd.Name = Name + "#" + dd.Name;
                        x.DrillDown.Insert(0, new TLColumnInfo<T>(Name, LookupTable, ReadOnly, Tag) { Index = Index });
                        x.Name = Name + "#" + x.Name;
                        if (c == this) x.ReadOnly = true; //ссылка сама на себя (иерархия) 
                    }
                return v;
            }
            return v;
        }

        protected override object FindInDrillDown(object row) { var v = LookupTable.FindIndex(GetRowValue(row)); return (v < 0) ? null : LookupTable[v];}
    }
    public enum TLRowState : byte { Original, Added, Changed, Deleted };

    internal static class TestTableList
    {
        public static void Test()
        {
            int rowCount = 1000000; //1mio
            var now = DateTime.Now;
            var msg = "Hellp from TEST!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!";
            object z;
            //on release
            TableList<long> tl = null;
            AppContext.RunBenchmark(() => {
            tl = new TableList<long>("0", "1", "2", "3", "4", "5", "6") { Capacity = rowCount};// { useIndex = true };
            for (var i = 0; i < rowCount; i++) {
                        //var x = tl.Add(i); x[1] = now; x[2] = msg; x[3] = int.MaxValue; x[4] = long.MaxValue; x[5] = decimal.MaxValue; x[6] = now;
                   // tl.Add(i, new object[7] { null, now, msg, int.MaxValue, long.MaxValue, decimal.MaxValue, now });
                }
                tl.AcceptChanges(true);
            }, 1, 0,  "TableList:     " + rowCount);
            //first call    :total = 766 ms. mem = 144 199 484 //with index and capacity:  total = 871 ms. mem = 176 558 412
            //second call   :total = 649 ms. mem = 184 814 448
            AppContext.RunBenchmark(() => { for (var i = 0; i < rowCount; i++) z = tl[tl.FindIndex(i)][3];} ,1,0, "TableListDirect(get):" + rowCount);                          //29 ms. mem = 8192
            AppContext.RunBenchmark(() => { for (var i = 0; i < rowCount; i++) z = tl[tl.FindIndex(i)][tl.GetColumnIndex("3")]; }, 1, 0, "TableListByName(get):" + rowCount);   //65 ms. mem = 8192
            AppContext.RunBenchmark(() => { var v = tl.Columns[3]; for (var i = 0; i < rowCount; i++) z = tl[tl.FindIndex(i), v]; }, 1, 0, "TableListByCol(get):" + rowCount);  //57 ms. mem = 8192
            /*var v = new System.Data.DataTable();v.BeginLoadData(); ... v.EndLoadData();*///total = 7 433 ms. mem = 446 724 640
            /*Dictionary<long, int> rowIndex = null; Dictionary<long, Dictionary<long, object>> dict = null; ... dict[rowIndex.GetValue(i, -1)][3];*/               //2 021 ms. mem = 425 202 028 get:65 ms.
            /*List<Tuple<long, DateTime, string, int, long, decimal, DateTime>> xx = null;//tuple readonly
            xx = new List<Tuple<long, DateTime, string, int, long, decimal, DateTime>>();...new Tuple<long, DateTime, string, int, long, decimal, DateTime>(...)*/  //218 ms. mem = 104 817 732 with index
            /*xx[rowIndex.GetValue(i, -1)].Item3;*/                                                                                                                 //30 ms. mem = 8192
            /*List<Tuple<long, DateTime, string, int, long, decimal, DateTime, Tuple<long, DateTime, string, int, long, decimal, DateTime>>> xxx = null;
            new Tuple<long, DateTime, string, int, long, decimal, DateTime, Tuple<long, DateTime, string, int, long, decimal, DateTime>>(..., Tuple.Create((long)i, now, msg, int.MaxValue, long.MaxValue, decimal.MaxValue, now)));
            xxx[rowIndex.GetValue(i, -1)].Rest.Item3;*/                                                                                                             // 419 ms. mem = 140 260 856 get:34 ms.
            /*List<object[]> lo = null; ... lo.Add(new object[7] { (long)i, now, msg, int.MaxValue, long.MaxValue, decimal.MaxValue, now }); ...lo[rowIndex.GetValue(i, -1)][3] */ //636 ms. mem = 180 817 960 get:30
            Dictionary<long, int> rowIndex = null;
            List<Tuple<long?, DateTime?, string, int?, long?, decimal?, DateTime?>> xx = null;
            AppContext.RunBenchmark(() => {
                xx = new List<Tuple<long?, DateTime?, string, int?, long?, decimal?, DateTime?>>();
                for (var i = 0; i < rowCount; i++) xx.Add(new Tuple<long?, DateTime?, string, int?, long?, decimal?, DateTime?>(i, now, msg, int.MaxValue, long.MaxValue, decimal.MaxValue, now));
                rowIndex = new Dictionary<long, int>(rowCount); for (var i = 0; i < rowCount; i++) rowIndex.Add(xx[i].Item1.Value, i);
            }, 1, 0, "List<Tuple?>:     " + rowCount);//total = 262 ms. mem = 136 820 548 !!!!!! для List<T>, где T class с полями как в tuple и инициализация через newT(){x1=i,...} -результат тот же
            /*var t0 = new TableList<int>("v", "w", "f");
            var r = t0.Add(1); r[1] = 111; r[2] = -1;
            r = t0.Add(2); r[1] = 222; r[2] = -2;
            r = t0.Add(3); r[1] = 333; r[2] = -3;
            //t0.RebuildIndex();//индекс не нужен, количество записей не большое
            var t1 = new TableList<int>("x", new ColumnInfo<int>("y", t0), new ColumnInfo("z"));
            r = t1.Add(1); r[1] = 1; r[2] = 12;
            r = t1.Add(2); r[1] = 2; r[2] = 22;
            r = t1.Add(3); r[1] = 3; r[2] = 32;
            //t1.RebuildIndex();////индекс не нужен, количество записей не большое
            var t2 = new TableList<long>("a", new ColumnInfo<int>("b", t1), new ColumnInfo<long>("c", null));
            (t2.Columns["c"] as ColumnInfo<long>).LookupTable = t2;
            var r2 = t2.Add(1); r2[1] = 1; r2[2] = 1L;
            for (var i = 2; i < 1000000; i++) { r2 = t2.Add(i); r2[1] = 2; r2[2] = 2L; }
            t2.RebuildIndex();
            t2.ColumnsExpanding = true;
            gridView.OptionsBehavior.ReadOnly = false;
            dataSource = t2;*/
            /*if (ot.Count < 5000) { grid.DataSource = ot; gridView.PopulateColumns(); } ;// до 5 000 отктырвает 1 сек, до 100 000 держит, но медленно
            else {  gridView.CustomUnboundColumnData += (s, e) => { if (e.IsGetData) e.Value = ((object[])e.Row)[(int)e.Column.Tag]; };
                    gridView.Columns.Clear();
                    foreach (var c in ot.Columns) { var clm = gridView.Columns.AddVisible(c.Key); clm.Tag = clm.VisibleIndex = c.Value.Index; clm.UnboundType = DevExpress.Data.UnboundColumnType.String; }
                    grid.DataSource = new DevExpress.Data.PLinq.PLinqServerModeSource() { Source = ot };// тут данные уже в строке не редактируются (видимо источник данных не поддерживает)
            }*///для pivot PLinqServerModeSource - не применяется, но и так быстро работает (но нужно указывать правильный тип колокни f.CellFormat.FormatType = f.ValueFormat.FormatType = FormatType.Numeric;)

            Console.ReadKey();
        }
    }
}
