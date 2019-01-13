using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace ETACO.CommonUtils
{
    public class OneSClient : DataProvider,  IDisposable
    {
        private BasicHttpBinding binding = new BasicHttpBinding();
        private ChannelFactory<IPortType> factory;
        private IPortType proxy;
        public override string ConnectionInfo { get { return factory?.Credentials.UserName.UserName+ "@" + factory?.Endpoint.Address.Uri; } }
        public OneSClient()
        {
            binding.Security.Mode = BasicHttpSecurityMode.TransportCredentialOnly;
            binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
            //binding.Security.Message.ClientCredentialType = BasicHttpMessageCredentialType.UserName;
            binding.MessageEncoding = WSMessageEncoding.Text;
            binding.MaxReceivedMessageSize = 104857600;
        }

        public OneSClient Connect(string url, string user, string pass = "")
        {
            CloseConnection();
            factory = new ChannelFactory<IPortType>(binding, new EndpointAddress(url));
            factory.Credentials.UserName.UserName = user;
            factory.Credentials.UserName.Password = pass;
            proxy = factory.CreateChannel();
            return this;
        }

        public override bool OpenConnection(Dictionary<string, string> _params)
        {
            var v = new Dictionary<string, string>(_params, StringComparer.InvariantCultureIgnoreCase);
            Connect(_params.GetValue("url"), _params.GetValue("user"), _params.GetValue("pass"));
            return true;
        }
        public override bool IsConnected { get { return proxy != null; } }
        public override void CloseConnection()
        {
            ((IClientChannel)proxy)?.Close();
            proxy = null;
            factory?.Close();
            factory = null;
        }

        public override void Read(bool newConn, IDataCommand command, Action<IDataRow> onInit, Action<IDataRow> onRow)
        {

            var cmd = "";
            var v = (BaseDataCommand)command;
            try
            {
                cmd = v.Sql.FormatStrEx(v.Parameters);
                if (AppContext.Log.UseTrace) AppContext.Log.Trace(cmd);
                OneSDataRow dr = null;
                int rowIndex = 0;
                foreach (var row in proxy.GetInfo(new GetInfoRequest() { Body = new GetInfoRequestBody() { query = cmd } }).Body.@return.Cast<List<string>>())
                {
                    if (dr == null) { dr = new OneSDataRow(row); onInit?.Invoke(dr); }
                    else onRow(dr.SetCurrent(row, rowIndex++));
                }
            }
            catch (Exception ex)
            {
                var info = GetExMessage(ex, cmd);
                if (info.IsEmpty()) throw; else throw new Exception(info);
            }
        }
        
        public override object Execute(DataTransaction dt, IDataCommand command)
        {
            var cmd = "";
            var v = (BaseDataCommand)command;
            try
            {
                cmd = v.Sql.FormatStrEx(v.Parameters);
                if (AppContext.Log.UseTrace) AppContext.Log.Trace(cmd);
                return proxy.Exec(new ExecRequest() { Body = new ExecRequestBody() { cmd = cmd } }).Body.@return;
            }
            catch(Exception ex)
            {
                var info = GetExMessage(ex, cmd);
                if (info.IsEmpty()) throw; else throw new Exception(info);
            }
        }

        public bool IsQuery(string cmd)
        {
            var v = AppContext.JSEval.Engine.RemoveComment(cmd + "").Trim();
            return v.StartsWith("ВЫБРАТЬ", StringComparison.OrdinalIgnoreCase) || v.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);
        }

        private string GetExMessage(Exception ex, string cmd)
        {
            try
            {
                var i = ex.Message.LastIndexOf("reason:");
                if (i > 0)
                {
                    var v = ex.Message.Substring(i + 8).Trim();
                    if (v.StartsWith("{(")) return v + "\r\n\r\n=>> " + cmd.Split('\n')[v.Substring(2, (v.IndexOf(',') == -1 ? v.IndexOf(')') : v.IndexOf(',')) - 2).GetIntFast() - 1].Trim();
                }
            }
            catch { }
            return null;
        }

        public void Dispose()
        {
            CloseConnection();
        }
        private class OneSDataRow : IDataRow
        {
            private string[] columnsName;
            private Type[] columnsType;
            private int columnsCount;
            private List<string> row;
            private static readonly Type S = typeof(string);
            private static readonly Type D = typeof(DateTime);
            private static readonly Type N = typeof(decimal);


            internal OneSDataRow(List<string> header)
            {
                row = header;
                columnsCount = header.Count;
                columnsName = new string[columnsCount];
                columnsType = new Type[columnsCount];
                for (var i = 0; i < columnsCount; i++)
                {
                    columnsName[i] = header[i].Substring(2);
                    var c = header[i][0];
                    columnsType[i] = c == 'n' ? N : (c == 'd' ? D : S);
                }
            }
            internal IDataRow SetCurrent(List<string> row, int ri) { this.row = row; RowIndex = ri; return this; }
            public string Name { get { return string.Empty; } }
            public int RowIndex { get; private set; }
            public object Get(int index) { if (IsNull(index)) return null; if (columnsType[index] == N) return GetDecimal(index); if (columnsType[index] == D) return GetDateTime(index); return GetString(index); }
            public bool GetBool(int index) { return bool.Parse(row[index]); }
            public int GetInt(int index) { return row[index].GetIntFast(); }
            public long GetLong(int index) { return row[index].GetLongFast(); }
            public decimal GetDecimal(int index) { return row[index].GetDecimalFast(); }
            public string GetString(int index) { return row[index]; }
            public DateTime GetDateTime(int index) { return DateTime.Parse(row[index]); }
            public byte[] GetBytes(int index) { return Encoding.UTF8.GetBytes(row[index]); }
            public int GetFieldIndex(string name) { return Array.IndexOf(columnsName, name); }
            public string GetFieldName(int index) { return columnsName[index]; }
            public Type GetFieldType(int index) { return columnsType[index]; }
            public int FieldCount { get { return columnsCount; } }
            public bool IsNull(int index) { return row[index].IsEmpty(); }
        }

        public static void Test(string url = "http://p-s-std01/DemoAccountingEduc/en_US/ws/jforbi", string user = "test", string pass = "")
        {
            using (var tc = new OneSClient().Connect(url, user, pass))
            {
                var sql  = tc.CreateCommand("ВЫБРАТЬ ЕСТЬNULL(БанковскиеВыписки.Дата,\"\") Дата, ЕСТЬNULL(БанковскиеВыписки.Проведен,\"\") Проведён, ЕСТЬNULL(БанковскиеВыписки.Организация.Наименование,\"\") Организация, " +
                " ЕСТЬNULL(БанковскиеВыписки.БанковскийСчет.НомерСчета,\"\") НомерСчета, ЕСТЬNULL(БанковскиеВыписки.БанковскийСчет.ВалютаДенежныхСредств.Код,\"\") Валюта, " +
                " ЕСТЬNULL(БанковскиеВыписки.Поступление,\"\") Поступление, ЕСТЬNULL(БанковскиеВыписки.Списание,\"\") Списание, ЕСТЬNULL(БанковскиеВыписки.НазначениеПлатежа,\"\") НазначениеПлатежа " +
                "ИЗ ЖурналДокументов.БанковскиеВыписки КАК БанковскиеВыписки");//ГДЕ	БанковскиеВыписки.Дата > ДАТАВРЕМЯ(2013, 01, 01) И БанковскиеВыписки.Дата < ДАТАВРЕМЯ(2016, 11, 01)";

                tc.Read(true, sql, null ,dr => { for (int i = 0; i < dr.FieldCount; i++) Console.WriteLine(dr.GetFieldName(i) + ":\t" + dr.Get(i)); }); Console.WriteLine();
               
                //var cmd = "Док = Документы.ОперацияБух.СоздатьДокумент(); Док.Дата = ТекущаяДата(); Док.Комментарий = \"Test\"; Док.СуммаОперации = 42; Док.Записать(); result=\"OK\"; ";
                var cmd = tc.CreateCommand("pp = Документы.ПлатежноеПоручение.СоздатьДокумент();" +
                    //"pp.ВидПлатежа=Справочники.ВидыОпераций.НайтиПоНаименованию(\"Обычный платеж\");" +
                    "pp.Дата = Дата(2017,02,07); " +
                    "pp.Контрагент = Справочники.Контрагенты.НайтиПоКоду(\"00-000008\"); " +
                    "pp.СчетКонтрагента = Справочники.БанковскиеСчета.НайтиПоРеквизиту(\"НомерСчета\", \"40810000000002030340\", ,pp.Контрагент); " +
                    "pp.Организация = Справочники.Организации.НайтиПоНаименованию(\"Комфорт-сервис\"); " +
                    "pp.СчетОрганизации = Справочники.БанковскиеСчета.НайтиПоРеквизиту(\"НомерСчета\", \"40710823230050064556\", ,pp.Организация); " +
                    "pp.СтатьяДвиженияДенежныхСредств = Справочники.СтатьиДвиженияДенежныхСредств.НайтиПоКоду(\"00-000021\");" +
                    "pp.СуммаДокумента = 100500; " +
                    "pp.СтавкаНДС = Перечисления.СтавкиНДС.НДС18; " +
                    "pp.СуммаНДС = 100500 /1.18 * 0.18; " +
                    "pp.НазначениеПлатежа = \"Привет из jforBi\";" +
                    "pp.Записать();result = \"OK\";");
                Console.WriteLine("Test exec: " + tc.Execute(null, cmd));
                Console.ReadKey();
            }
        }
    }

    [CollectionDataContract(Name = "TRow", Namespace = "http://www.etaco.ru", ItemName = "items")]
    [Serializable]
    public class TRow : List<string> { }

    [CollectionDataContract(Name = "TRowList", Namespace = "http://www.etaco.ru", ItemName = "items")]
    [Serializable]
    public class TRowList : List<TRow> { }

    [ServiceContract(Namespace = "http://www.etaco.ru")]
    public interface IPortType
    {
        [OperationContract]
        GetInfoResponse GetInfo(GetInfoRequest request);
        [OperationContract]
        ExecResponse Exec(ExecRequest cmd);
    }

    [MessageContract(IsWrapped = false)]
    public class GetInfoRequest
    {
        [MessageBodyMember(Name = "GetInfo", Order = 0)]
        public GetInfoRequestBody Body;
    }

    [DataContract(Namespace = "http://www.etaco.ru")]
    public class GetInfoRequestBody
    {
        [DataMember(EmitDefaultValue = false, Order = 0)]
        public string query;
    }

    [MessageContract(IsWrapped = false)]
    public class GetInfoResponse
    {
        [MessageBodyMember(Name = "GetInfoResponse", Order = 0)]
        public GetInfoResponseBody Body;
    }


    [DataContract(Namespace = "http://www.etaco.ru")]
    public class GetInfoResponseBody
    {
        [DataMember(EmitDefaultValue = false, Order = 0)]
        public TRowList @return;
    }

    [MessageContract(IsWrapped = false)]
    public class ExecRequest
    {
        [MessageBodyMember(Name = "Exec", Order = 0)]
        public ExecRequestBody Body;
    }

    [DataContract(Namespace = "http://www.etaco.ru")]
    public class ExecRequestBody
    {
        [DataMember(EmitDefaultValue = false, Order = 0)]
        public string cmd;
    }

    [MessageContract(IsWrapped = false)]
    public class ExecResponse
    {
        [MessageBodyMember(Name = "ExecResponse", Order = 0)]
        public ExecResponseBody Body;
    }


    [DataContract(Namespace = "http://www.etaco.ru")]
    public class ExecResponseBody
    {
        [DataMember(EmitDefaultValue = false, Order = 0)]
        public string @return;
    }
}
/*
test => http://p-s-std01/DemoAccountingEduc/en_US/ws/jforbi?wsdl             //user="test" pwd="" 
настройка web service в 1c http://infostart.ru/public/275820/

/* ======= web-service ==================
Функция Exec(cmd) Экспорт
	Перем result;
	Execute(cmd);
	Возврат result;	
КонецФункции
		
Функция GetInfo(query) Экспорт
	
result =  ФабрикаXDTO.Создать(ФабрикаXDTO.Тип("http://www.etaco.ru", "TRowList"));
TRow = ФабрикаXDTO.Тип("http://www.etaco.ru", "TRow");


Запрос = Новый Запрос;
Запрос.Текст = query;
qr = Запрос.Выполнить();

cols = qr.Колонки;
colCount = cols.Количество();

row = ФабрикаXDTO.Создать(TRow);
Для i = 0 По colCount-1 Цикл 
	row.items.Добавить(GetColType(cols.Получить(i).ТипЗначения) +":"+ cols.Получить(i).Имя);
КонецЦикла;
result.items.Добавить(row);

res = qr.Выбрать();
Пока res.Следующий() Цикл	 
	 row = ФабрикаXDTO.Создать(TRow);
	 Для i = 0 По colCount-1 Цикл
        row.items.Добавить(res.Получить(i));
     КонецЦикла;
	 result.items.Добавить(row);
КонецЦикла;
 
Возврат result;
 
КонецФункции

Функция GetColType(type)
	If type.ContainsType(Type("Date")) Then Возврат "d";
    ElsIf type.ContainsType(Type("Number")) Then Возврат "n";
	Else Возврат "s";
    EndIf;
КонецФункции

////// XDTO-package  
     
<xs:schema xmlns:tns="http://www.sample-package.org" xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="http://www.sample-package.org" attributeFormDefault="unqualified" elementFormDefault="qualified">
	<xs:complexType name="TRow">
		<xs:sequence>
			<xs:element name="items" type="xs:string" nillable="true" maxOccurs="100"/>
		</xs:sequence>
	</xs:complexType>
	<xs:complexType name="TRowList">
		<xs:sequence>
			<xs:element name="items" type="tns:TRow" maxOccurs="1000"/>
		</xs:sequence>
	</xs:complexType>
</xs:schema>

////// web.config

<?xml version="1.0" encoding="UTF-8"?>
<configuration>
    <system.webServer>
        <handlers>
            <add name="1C Web-service Extension" path="*" verb="*" modules="IsapiModule" scriptProcessor="C:\Program Files (x86)\1cv8t\8.3.6.2014\bin\wsisapit.dll" resourceType="Unspecified" requireAccess="None" />
        </handlers>
    </system.webServer>
<system.web>
<pages validateRequest="false" />
<httpRuntime requestPathInvalidCharacters="" />
</system.web> 
</configuration> 

////////////////////////////////////////// 1C CODE ///////////////////////////////////////
// Действия со строкой табличной части. 
Ик=ДокОперация.Движения.ЕПСБУ.Добавить();
Ик.СчетДт=ПланыСчетов.ЕПСБУ.НайтиПоКоду("401.20");
Ик.СчетКт=ПланыСчетов.ЕПСБУ.НайтиПоКоду("304.05") ;
Ик.КБКДт = Справочники.КБК.НайтиПоНаименованию("71007024362100002");
Ик.КБККт = Справочники.КБК.НайтиПоНаименованию("71007024362100002");
Ик.Период=ДатаДокумента;
Ик.Учреждение= Справочники.Организации.НайтиПоНаименованию("Организация");
Ик.Сумма=555;
Ик.СубконтоДт=Справочники.КОСГУ.НайтиПоНаименованию("Заработная плата");
Ик.СубконтоКт=Справочники.КОСГУ.НайтиПоНаименованию("Заработная плата");
 
ДокОперация.Записать(); 
///////////////////

Док = Документы.ОперацияБух.СоздатьДокумент();
ДатаЗаписи = ТекущаяДата();
Док.Дата   = ДатаЗаписи;
Док.Организация=Организация;
Док.Записать();
       
Для Каждого СтрМатериалы Из ТабЗагрузка Цикл 
    Если СтрМатериалы.НашДол>0 Тогда
        СпрКонтрагент = Справочники.Контрагенты.НайтиПоНаименованию(СтрМатериалы.Контрагент);
        Если СпрКонтрагент=Неопределено Тогда
            Сообщить("Не найдена "+СтрМатериалы.КодКонтрагента+" "+СтрМатериалы.Контрагент);
            Продолжить;
        КонецЕсли;
        СпрДоговорКонт       = СпрКонтрагент.ОсновнойДоговорКонтрагента;
        НаборЗаписей         = РегистрыБухгалтерии.Хозрасчетный.СоздатьНаборЗаписей();
        НаборЗаписей.Отбор.Регистратор.Установить(Док.Ссылка); 
        Движение             = НаборЗаписей.Добавить(); 
        Движение.Регистратор = Док.Ссылка; 
        Движение.Период      = ДатаЗаписи;             
        Если СтрМатериалы.НашДол>0 Тогда
            Движение.СубконтоКт.Контрагенты = СпрКонтрагент;
            Движение.СубконтоКт.Договоры = СпрДоговорКонт;
            Движение.Сумма       = СтрМатериалы.НашДол;
            Движение.СчетКт      = ПланыСчетов.Хозрасчетный.РасчетыСПоставщиками;
            Движение.СчетДт      = ПланыСчетов.Хозрасчетный.Вспомогательный;
            НаборЗаписей.Записать();
        КонецЕсли;
    КонецЕсли;    
КонецЦикла;
Форма = Док.ПолучитьФорму();
Форма.Открыть();
*/
