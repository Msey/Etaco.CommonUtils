using System;
using System.Data;
using System.Data.OracleClient;

namespace ETACO.CommonUtils
{
    /// <summary> Содержит расширения для работы с OraDataAccess </summary>
    public static class OraDataAccessExtensions
    {
        /// <summary> Получение следующего значения id (-db:next_id) </summary>
        public static string GetNextId(this OraDataAccess oda)
        {
            return oda.ExecuteScalar(AppContext.Config["db", "next_id"]) + "";
        }

        /// <summary> Имеется ли у текущего пользователя данная роль </summary>
        public static bool HasRole(this OraDataAccess oda, string role)
        {
            //select count(*) from user_role_privs where granted_role = :p0
            return oda.ExecuteScalar("select nvl2(sr.role, 1,0) from dual left join session_roles sr on sr.role=:p0", role) + "" == "1";
        }

        /// <summary> Отключить всех пользователей имена которых удовлетворяют данной маске </summary>
        /// <remarks> Выполненять с правами System </remarks>
        public static void KillThemAll(this OraDataAccess oda, string mask)
        {
            oda.ExecuteNonQuery("begin  for killRec in (select sid, serial# FROM gv$session s WHERE s.username like '{0}') loop " +
                                " execute immediate 'ALTER SYSTEM KILL SESSION '''||killRec.sid ||','|| killRec.serial#||''' IMMEDIATE'; end loop; end;".FormatStr(mask));
        }

        /// <summary> Получить список первичных ключей для данной таблицы </summary>
        /// <remarks> Выполнять из под владельца схемы </remarks>
        public static DataTable GetPrimaryKeys(this OraDataAccess oda, string tableName)
        {
            var sql = @"select ucc.column_name, ucc.position from user_constraints uc, user_cons_columns ucc
                        where uc.constraint_type ='P' and uc.table_name = :p0 and ucc.constraint_name=uc.constraint_name
                        order by ucc.position";
            return oda.GetQueryResult(sql, tableName.ToUpper());
        }

        /// <summary> Получить список внешних ключей для данной таблицы </summary>
        /// <remarks> Выполнять из под владельца схемы </remarks>
        public static DataTable GetForeignKeys(this OraDataAccess oda, string tableName)
        {
            var sql = @"select ucc.column_name, ucc2.table_name ref_table, ucc2.column_name ref_column_name, ucc2.position ref_position 
                        from user_constraints uc, user_cons_columns ucc, user_cons_columns ucc2
                        where uc.constraint_type ='R' and uc.table_name = :p0 and ucc.constraint_name=uc.constraint_name and ucc2.constraint_name = uc.r_constraint_name";
            return oda.GetQueryResult(sql, tableName.ToUpper());
        }

        /// <summary> Получить список ограничений для данной таблицы </summary>
        /// <remarks> Выполнять из под владельца схемы </remarks>
        public static DataTable GetConstraints(this OraDataAccess oda, string tableName)
        {
            var sql = @"select ucc.column_name, uc.search_condition from user_constraints uc, user_cons_columns ucc
                        where uc.constraint_type ='C' and uc.table_name = :p0 and ucc.constraint_name=uc.constraint_name";
            return oda.GetQueryResult(sql, tableName.ToUpper());
        }

        /// <summary> Получить оценку количества записей в таблице, по которой производится сбор статистики </summary>
        /// <remarks> Выполнять из под владельца схемы </remarks>
        /// <remarks> Для получения точного значения нужно сначала собрать статистику, либо использовать 'select count(id) from mytable' (где id первичный ключ)</remarks>
        /// <returns> -1 - если таблица с данным именем не найдена, иначе - оценка числа строк в таблице</returns>
        public static decimal EstimateRowCount(this OraDataAccess oda, string tableName)
        {
            var v = oda.ExecuteScalar("select num_rows from user_tables where table_name = :p0", (tableName + "").ToUpper()); //можно использовать all_tables, но тогда нужно фильтровать по owner
            return v == null ? -1 : (decimal)v;
        }
    }
}
