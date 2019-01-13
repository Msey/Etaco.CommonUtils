using System;

namespace ETACO.CommonUtils
{
    /// <summary> Содержит расширения для работы с PGSQLDataAccess </summary>
    public static class PGSQLDataAccessExtensions
    {
        /// <summary> Имеется ли у текущего пользователя данная роль </summary>
        public static bool HasRole(this PGSQLDataAccess pgda, string role)
        {
            return Convert.ToInt32(pgda.ExecuteScalar(
                "SELECT Count(u.usesysid) FROM pg_user u, pg_group g, pg_auth_members um\n" +
                "WHERE u.usename = user AND Upper(g.groname) = :p0 AND um.member = u.usesysid AND um.roleid = g.grosysid;",
                role.ToUpperInvariant())) > 0;
        }

        /// <summary> Создать LargeObject </summary>
        public static int CreateLargeObject(this PGSQLDataAccess pgda, int loid = 0)
        {
            return Convert.ToInt32(pgda.ExecuteScalar("select lo_create(:p0)", loid));
        }

        /// <summary> Права на LargeObject </summary>
        public static void GrantAndOwnLargeObject(this PGSQLDataAccess pgda, int loid, string Role, string Owner = null)
        {
            pgda.ExecuteNonQuery("GRANT ALL ON LARGE OBJECT {0} TO {1}; ".FormatStr(loid, Role));
            pgda.ExecuteNonQuery("ALTER LARGE OBJECT {0} OWNER TO {1}; ".FormatStr(loid, Owner ?? Role));
        }

        /// <summary> Удалить LargeObject </summary>
        public static bool DeleteLargeObject(this PGSQLDataAccess pgda, int loid)
        {
            return Convert.ToInt32(pgda.ExecuteScalar("SELECT lo_unlink(:p0)", loid)) == 1;
        }

        /// <summary> Чтение LargeObject в поток</summary>
        public static bool ReadLargeObject(this PGSQLDataAccess pgda, int loid, System.IO.Stream io, int blockSize = 512 * 1024)
        {
            var result = false;
            pgda.UseTransaction((dc) =>
            {
                var fd = Convert.ToInt32(new PGSQLCommand("SELECT lo_open(:p0,:p1)", loid, 0x40000).ExecuteScalar(dc));
                try
                {
                    int len;
                    try
                    {
                        var cmd = new PGSQLCommand("SELECT loread(:p0, :p1)", fd, blockSize);
                        do
                        {
                            var buff = (byte[])cmd.ExecuteScalar(dc);
                            len = buff.Length;
                            if (len > 0)
                                io.Write(buff, 0, len);
                        }
                        while (len == blockSize);
                        result = true;
                    }
                    catch//(Exception ex)
                    {
                        result = false;
                    }
                }
                finally
                {
                    new PGSQLCommand("SELECT lo_close(:p0)", fd).ExecuteNonQuery(dc);
                }
            });
            return result;
        }

        /// <summary> Запись потока в LargeObject</summary>
        public static bool WriteLargeObject(this PGSQLDataAccess pgda, int loid, System.IO.Stream io, int blockSize = 512 * 1024)
        {
            var result = false;
            pgda.UseTransaction((dc) =>
            {
                var fd = Convert.ToInt32(new PGSQLCommand("SELECT lo_open(:p0,:p1)", loid, 0x20000).ExecuteScalar(dc));
                try
                {
                    var buff = new byte[blockSize];
                    int len;
                    try
                    {
                        new PGSQLCommand("SELECT lo_truncate(:p0, 0)", fd).ExecuteNonQuery(dc);
                        var cmd = new PGSQLCommand("SELECT lowrite(:p0, :p1)", fd, buff);
                        do
                        {
                            len = io.Read(buff, 0, blockSize);
                            if (len != blockSize)
                            {
                                var newbuff = new byte[len];
                                Array.Copy(buff, newbuff, len);
                                buff = newbuff;
                                cmd["p1"] = newbuff;
                            }
                            cmd.ExecuteScalar(dc);
                        }
                        while (len == blockSize);

                        result = true;
                    }
                    catch//(Exception ex)
                    {
                        result = false;
                    }
                }
                finally
                {
                    new PGSQLCommand("SELECT lo_close(:p0)", fd).ExecuteNonQuery(dc);
                }
            });
            return result;
        }
    }
}
