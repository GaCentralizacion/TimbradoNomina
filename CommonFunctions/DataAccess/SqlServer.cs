using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

namespace CommonFunctions.DataAccess
{
    public class SqlServer
    {

        string connectionString = string.Empty;

        public string ConnectionString
        {
            get { return connectionString; }
            set { connectionString = value; }
        }


        public SqlServer()
        {


        }


        public int ExecuteNonQueryProcedure(string commandText, params SqlParameter[] commandParameters)
        {
            int affectedRows = 0;
            using (var connection = new SqlConnection(connectionString))
            {
                using (var command = new SqlCommand(commandText, connection))
                {
                    connection.Open();
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddRange(commandParameters);
                    affectedRows = command.ExecuteNonQuery();
                }
            }
            return affectedRows;
        }



        public int ExecuteNonQuery(string commandText, params SqlParameter[] commandParameters)
        {
            int affectedRows = 0;
            using (var connection = new SqlConnection(connectionString))
            {
                using (var command = new SqlCommand(commandText, connection))
                {
                    command.CommandType = CommandType.Text;
                    command.Parameters.AddRange(commandParameters);
                    affectedRows = command.ExecuteNonQuery();
                }
            }
            return affectedRows;
        }




        public DataSet ExecuteQueryProcedure(string commandText, params SqlParameter[] parameters)
        {
            
                using (var connection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(commandText, connection))
                {
                    DataSet ds = new DataSet();
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddRange(parameters);
                    SqlDataAdapter da = new SqlDataAdapter(command);
                    da.Fill(ds);
                    connection.Close();
                    return ds;
                }
     
        }


        public DataSet ExecuteQuery(string commandText, params SqlParameter[] parameters)
        {
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(commandText, connection))
            {
                DataSet ds = new DataSet();
                command.CommandType = CommandType.Text;
                command.Parameters.AddRange(parameters);
                SqlDataAdapter da = new SqlDataAdapter(command);
                da.Fill(ds);
                connection.Close();
                return ds;
            }
        }



    }
}
