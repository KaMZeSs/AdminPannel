using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dapper;

namespace AdminPannel
{
    public static class SqlConnectionExtensions
    {
        public static async Task<IEnumerable<dynamic>> QueryAsExpandoAsync(
            this IDbConnection connection, string sql, object? param = null)
        {
            var results = await connection.QueryAsync<dynamic>(sql: sql, param: param);

            var list = new List<ExpandoObject>(results.Count());

            //await Task.WhenAll(results.AsParallel().Select(async row =>
            //{
            //    dynamic expando = new ExpandoObject();
            //    var expandoDict = (IDictionary<string, object>)expando;
            //    foreach (var property in row)
            //    {
            //        expandoDict[property.Key] = property.Value;
            //    }

            //    list.Add(expando);
            //}));

            foreach (var row in results)
            {
                dynamic expando = new ExpandoObject();
                var expandoDict = (IDictionary<string, object>)expando;

                foreach (var property in row)
                {
                    expandoDict[property.Key] = property.Value;
                }
                list.Add(expando);
            }

            return list;
        }

        public static async Task<IEnumerable<dynamic>> QueryFirstAsExpandoAsync(
            this IDbConnection connection, string sql, object? param = null)
        {
            var result = await connection.QueryFirstAsync<dynamic>(sql: sql, param: param);

            dynamic expando = new ExpandoObject();
            var expandoDict = (IDictionary<string, object>)expando;
            foreach (var property in result)
            {
                expandoDict[property.Key] = property.Value;
            }

            return expando;
        }
    }

}
