using System;
using System.Collections.Generic;
using System.Data;

namespace ETACO.CommonUtils
{
    public abstract class AbstractExchangeManager
    {
        /// <summary>Имя свойства содержащего url элемента</summary>
        public const string Href = "DAV:href";
        /// <summary>Имя свойства содержащего имя элемента</summary>
        public const string DisplayName = "DAV:displayname";
        /// <summary>Имя свойства содержащего признак папки для элемента</summary>
        public const string IsFolder = "DAV:isfolder";
        /// <summary>Имя свойства содержащего число потомков элемента</summary>
        public const string ChildCount = "DAV:childcount";

        /// <summary>Получение элементов находящихся в папке</summary>
        protected abstract void LoadItemList(string url, Action<object[]> onItem, string sql, string[] fields);
        
        /// <summary>Получение свойств элемента</summary>
        /// <param name="url">URL элемента</param>
        /// <param name="fields">Список возвращаемых полей элемента (если пуст то возвращается полный список)</param>
        public abstract List<ItemProperty> GetProperties(string url, params string[] fields);

        /// <summary>Получение свойств элемента</summary>
        /// <param name="url">URL элемента</param>
        /// <param name="fields">Список возвращаемых полей элемента (если пуст то возвращается полный список)</param>
        public abstract Dictionary<string, object>  GetPropertiesDict(string url, params string[] fields);
        
        /// <summary>Создание элемента</summary>
        protected abstract void CreateItem(string url, List<ItemProperty> forCreate, bool isFolder);

        /// <summary>Обновление свойств элемента (add, set, remove)</summary>
        /// <param name="url">URL элемента</param>
        /// <param name="forUpdate">Список параметров на добавление/обновление</param>
        /// <param name="forDelete">Список параметров на удаление</param>
        public abstract void UpdateItem(string url, List<ItemProperty> forUpdate = null, List<string> forDelete = null);

        /// <summary>Удаление элемента</summary>
        /// <param name="url">URL элемента</param>
        public abstract void DeleteItem(string url);
        
        /// <summary>Проверка существования элемента</summary>
        /// <param name="url">URL элемента</param>
        public bool Exist(string url)
        {
            try
            {
                GetProperty(url, "DAV:href");
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <summary>Получение элементов находящихся в папке</summary>
        /// <param name="url">URL родительской папки</param>
        /// <param name="onItem">Обработчик получения нового элемента (если = null результат добавляется в returns)</param>
        /// <param name="filter">Фильтр поиска элементов</param>
        /// <param name="sort">Параметры сортировки элементов</param>
        /// <param name="fields">Список возвращаемых полей элементов (если пуст, то используются "DAV:href", "DAV:displayname", "DAV:isfolder", "DAV:childcount")</param>
        /// <returns> Таблица с запрашиваемыми свойствами элементов (если onItem не пуст, то возвращается пустой список)</returns>
        /// <remarks> В результирующей таблице могут содержаться столбцы с именами вида DAV:*.</remarks>
        /// <remarks> Для работы с такими полями в запросах к таблице нужно использовать [] для экранирования спец символов.</remarks>
        /// <remarks> Синтаксис будет иметь вид table.Select("[DAV:displayname]='text'")</remarks>
        public DataTable GetItemList(string url, Action<object[]> onItem = null, string filter = "", string sort = "", params string[] fields)
        {
            var result = new DataTable();
            fields = (fields == null || fields.Length == 0) ? new string[] { Href, DisplayName, IsFolder, ChildCount } : fields;
            Array.ForEach(fields, (v) => result.Columns.Add(v));
            onItem = onItem == null ? (v) => result.Rows.Add(v) : onItem;
            filter = filter.IsEmpty() ? "" : " where {0} ".FormatStr(filter);
            sort = sort.IsEmpty() ? "" : " orderby {0} ".FormatStr(sort);

            filter = filter.Replace("'", "\"");
            sort = sort.Replace("'", "\"");
            var sql = "select {1} from \"{0}\" {2} {3}".FormatStr(url, "\"" + string.Join("\",\"", fields) + "\"", filter, sort);
            
            LoadItemList(url, onItem, sql, fields);
            result.AcceptChanges();
            return result;
        }

        /// <summary>Получение списка подпапок</summary>
        /// <param name="url">URL родительской папки</param>
        public DataTable GetFolderList(string url)
        {
            return GetItemList(url, null, "'DAV:isfolder' = true");
        }

        /// <summary>Создание элемента</summary>
        /// <param name="url">URL элемента</param>
        /// <param name="forCreate">Свойства создаваемого элемента</param>
        public void CreateItem(string url, List<ItemProperty> forCreate)
        {
            if (Exist(url)) throw new Exception("Item '{0}' already exists.".FormatStr(Uri.UnescapeDataString(url)));
            if (forCreate == null || forCreate.Count == 0) throw new Exception("Properties for create item '{0}' is empty.".FormatStr(Uri.UnescapeDataString(url)));
            var isFolder = forCreate != null && forCreate.Exists((v) => v.Value + "" == "urn:content-classes:folder");
            CreateItem(url, forCreate, isFolder);
        }

        /// <summary>Создание элемента типа папка</summary>
        /// <param name="url">URL элемента</param>
        public void CreateFolder(string url)
        {
            var forCreate = new List<ItemProperty>();
            forCreate.Add(new ItemProperty("DAV:contentclass", "urn:content-classes:folder", "string"));
            forCreate.Add(new ItemProperty("http://schemas.microsoft.com/exchange/outlookfolderclass", "IPF.Note", "string"));
            CreateItem(url, forCreate);
        }

        /// <summary>Создание элемента типа документ</summary>
        /// <param name="url">URL элемента</param>
        public void CreateDocument(string url)
        {
            var forCreate = new List<ItemProperty>();
            forCreate.Add(new ItemProperty("DAV:contentclass", "urn:content-classes:message", "string"));
            forCreate.Add(new ItemProperty("http://schemas.microsoft.com/exchange/outlookmessageclass", "IPM.Document", "string"));
            CreateItem(url, forCreate);
        }

        /// <summary>Получение свойства элемента</summary>
        /// <param name="url">URL элемента</param>
        /// <param name="name">имя свойства</param>
        /// <remarks> обёртка над GetProperties</remarks>
        public object GetProperty(string url, string name)
        {
            return GetProperties(url, name)[0].Value;
        }

        /// <summary>Обновление свойств элемента (add, set)</summary>
        /// <param name="url">URL элемента</param>
        /// <param name="name">имя свойства</param>
        /// <param name="value">значение</param>
        /// <param name="type">наименование типа</param>
        /// <remarks> обёртка над UpdateItem</remarks>
        public void SetProperty(string url, string name, object value, string type = null)
        {
            UpdateItem(url, new List<ItemProperty>() { new ItemProperty(name, value, type) });
        }

        /// <summary>Обновление свойств элемента (add, set)</summary>
        /// <param name="url">URL элемента</param>
        /// <param name="name">имя свойства</param>
        /// <remarks> обёртка над UpdateItem</remarks>
        public void DeleteProperty(string url, string name)
        {
            UpdateItem(url, null, new List<string>() { name});
        }

        /// <summary>Список полей по умолчанию</summary>
        public static string[] GetDefaultColumns()
        {
            return new[] { Href, DisplayName, IsFolder, ChildCount };
        }
    }

    /// <summary> Свойство элемента папки </summary>
    public class ItemProperty
    {
        public string Name { get; private set; }
        public object Value { get; private set; }
        public string Type { get; private set; }

        public ItemProperty(string name, object value, string type = "")
        {
            Name = name;
            Value = value;
            Type = type;
        }
    }
}
