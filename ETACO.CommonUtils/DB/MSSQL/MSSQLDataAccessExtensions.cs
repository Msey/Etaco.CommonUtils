using System.Data;

namespace ETACO.CommonUtils
{
    public static class MSSQLDataAccessExtensions
    {

        /// <summary> Получить список логинов и пользователей </summary>
        public static DataTable GetLoginAndUser(this MSSQLDataAccess msda)
        {
            return msda.GetQueryResult(@"SELECT sp.name [login], dp.name [user] FROM sys.server_principals sp, sys.database_principals dp 
                                        where sp.type_desc in ('WINDOWS_LOGIN', 'SQL_LOGIN') and sp.is_disabled!= 1 and not sp.name LIKE 'NT %' and dp.sid = sp.sid");
        }

        /// <summary> Получить список пользователей и ролей </summary>
        public static DataTable GetUserAndRole(this MSSQLDataAccess msda)
        {
            return msda.GetQueryResult(@"select mp.name [user], rp.name [role] from sys.database_role_members drm
                                        join sys.database_principals rp on (drm.role_principal_id = rp.principal_id)
                                        join sys.database_principals mp on (drm.member_principal_id = mp.principal_id)");
        }
        
        /// <summary> Получить список логинов, пользователей и ролей </summary>
        public static DataTable GetLoginAndRole(this MSSQLDataAccess msda)
        {
            return msda.GetQueryResult(@"select [login], [user], (select name from sys.database_principals where principal_id= drm.role_principal_id) [role]
                            from (
                            SELECT dp.principal_id user_id, sp.name [login], dp.name [user], sp.type_desc [type] 
                            FROM sys.server_principals sp, sys.database_principals dp 
                            where sp.type_desc in ('WINDOWS_LOGIN', 'SQL_LOGIN') and sp.is_disabled!= 1 and not sp.name LIKE 'NT %' and dp.sid = sp.sid) x, sys.database_role_members  drm
                            where user_id = drm.member_principal_id 
                            order by [type], [login]");
        }

        /// <summary> Получить список таблиц </summary>
        public static DataTable GetTables(this MSSQLDataAccess msda)
        {
            return msda.GetQueryResult("select * from information_schema.tables");
        }

        /// <summary> Получить список ключей для таблицы </summary>
        public static DataTable GetTableConstraints(this MSSQLDataAccess msda, string tableName)
        {
            return msda.GetQueryResult(@"select tc.constraint_name, tc.constraint_type, ccu.column_name from information_schema.table_constraints tc 
                                        join information_schema.constraint_column_usage ccu ON tc.constraint_name = ccu.constraint_name WHERE  tc.table_name = @p0", tableName);
        }
    }
}
