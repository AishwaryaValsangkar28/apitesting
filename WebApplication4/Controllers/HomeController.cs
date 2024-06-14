using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace WebApplication4.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HomeController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public HomeController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        //Page 1 - site name, location, employee count, min and max
        [HttpGet("GetAllSitesInfo")]
        public IActionResult GetAllSites([FromQuery] int hours)
        {
            List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();

            try
            {
                using (SqlConnection connection = CreateSqlConnection())
                {
                    connection.Open();
                    Console.WriteLine("Connection successful");

                    Console.WriteLine(hours);

                    // First query to get the total employee count per facility
                    SqlCommand command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT IF_IF_raumzone.rzdesc AS Facility, COUNT(*) AS TotalCount 
                        FROM IF_IF_empcurronl
                        INNER JOIN IF_IF_raumzone ON IF_IF_empcurronl.AccessZoneDOR = IF_IF_raumzone.Guid
                        WHERE IF_IF_raumzone.rzrzwonl = 'B' 
                        AND IF_IF_raumzone.rznr NOT IN (281, 309, 310)
                        GROUP BY IF_IF_empcurronl.AccessZoneDOR, IF_IF_raumzone.rzdesc
                        ORDER BY IF_IF_raumzone.rzdesc";

                    Console.WriteLine("Executing query to get total employee counts per facility:");
                    Console.WriteLine(command.CommandText);

                    SqlDataReader reader = command.ExecuteReader();

                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            var record = new Dictionary<string, object>
                    {
                        { "location", reader["Facility"] },
                        { "city", null },
                        { "Emp_Count", reader["TotalCount"] }
                    };
                            results.Add(record);
                        }
                    }
                    else
                    {
                        Console.WriteLine("No data found in the first query.");
                    }
                    reader.Close();

                    // Second query to get min and max values per facility
                    command.CommandText = @"
                        WITH cte AS (
                            SELECT 
                                IF_IF_raumzone.rznr,
                                IF_IF_raumzone.rzdesc, 
                                DATEPART(HOUR, eclbtimestamp) AS hour, 
                                COUNT(*) AS TotalCount 
                            FROM IF_IF_empcurronl
                            JOIN IF_IF_raumzone ON IF_IF_empcurronl.AccessZoneDOR = IF_IF_raumzone.Guid
                            WHERE eclbtimestamp >= DATEADD(HOUR, @Hours, GETDATE())
                            AND IF_IF_raumzone.rznr NOT IN (281, 309, 310)
                            GROUP BY DATEPART(HOUR, eclbtimestamp), IF_IF_raumzone.rznr, IF_IF_raumzone.rzdesc
                        )
                        SELECT 
                            rzdesc AS Facility, 
                            MAX(TotalCount) AS MaxCount, 
                            MIN(TotalCount) AS MinCount
                        FROM cte
                        GROUP BY rzdesc";

                    Console.WriteLine(command.CommandText);

                    command.Parameters.AddWithValue("@Hours", -hours);

                    reader = command.ExecuteReader();

                    // Dictionary to store min and max values keyed by facility name
                    Dictionary<string, (int MinCount, int MaxCount)> minMaxValues = new Dictionary<string, (int, int)>();

                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            minMaxValues[reader["Facility"].ToString()] = ((int)reader["MinCount"], (int)reader["MaxCount"]);
                        }
                    }
                    else
                    {
                        Console.WriteLine("No data found in the second query.");
                    }
                    reader.Close();

                    // Merge min and max values into the results list
                    foreach (var record in results)
                    {
                        string facility = record["location"].ToString();
                        if (minMaxValues.ContainsKey(facility))
                        {
                            record["min"] = minMaxValues[facility].MinCount;
                            record["max"] = minMaxValues[facility].MaxCount;
                        }
                        else
                        {
                            record["min"] = 0;
                            record["max"] = 0;
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                Console.WriteLine($"SQL Error: {sqlEx.Message}");
                return StatusCode(500, "A database error occurred while processing your request.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, "An error occurred while processing your request.");
            }

            if (results.Count > 0)
            {
                return Ok(new { statuscode = 200, data = results });
            }
            else
            {
                return NotFound("No data found.");
            }
        }

        //Page 1 - india and overseas count
        [HttpGet("GetIndiaAndOverseasCounts")]
        public IActionResult GetIndiaAndOverseasCounts()
        {
            int indiaCount = 0;
            int overseasCount = 0;

            try
            {
                using (SqlConnection connection = CreateSqlConnection())
                {
                    connection.Open();
                    Console.WriteLine("Connection successful");

                    // Query 1: India Count
                    SqlCommand command = connection.CreateCommand();
                    command.CommandText = @"
                        WITH ResultCTE AS (
                            SELECT IF_IF_raumzone.rzdesc AS Facility, COUNT(*) AS TotalCount
                            FROM IF_IF_empcurronl
                            INNER JOIN IF_IF_raumzone ON IF_IF_empcurronl.AccessZoneDOR = IF_IF_raumzone.Guid
                            WHERE IF_IF_raumzone.rzrzwonl = 'B' 
                            AND IF_IF_raumzone.rznr NOT IN (281, 309, 310)
                            GROUP BY IF_IF_raumzone.rzdesc
                        )
                        SELECT COUNT(*) AS 'INDIA_count'
                        FROM ResultCTE";
                    indiaCount = (int)command.ExecuteScalar();

                    // Query 2: Overseas Count
                    command.CommandText = @"
                        WITH ResultCTE AS (
                            SELECT IF_IF_raumzone.rzdesc AS Facility, COUNT(*) AS TotalCount
                            FROM IF_IF_empcurronl
                            INNER JOIN IF_IF_raumzone ON IF_IF_empcurronl.AccessZoneDOR = IF_IF_raumzone.Guid
                            WHERE IF_IF_raumzone.rzrzwonl = 'B' 
                            AND IF_IF_raumzone.rznr IN (281, 309, 310)
                            GROUP BY IF_IF_raumzone.rzdesc
                        )
                        SELECT COUNT(*) AS 'Overseas_count'
                        FROM ResultCTE";
                    overseasCount = (int)command.ExecuteScalar();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, "An error occurred while processing your request.");
            }

            return Ok(new
            {
                statuscode = 200,
                data = new[]
        {
            new { name = "India", value = indiaCount },
            new { name = "Overseas", value = overseasCount }
        }
            });
        }

        [HttpGet("EmployeeCountBySite")]
        public IActionResult GetEmployeeCountBySite()
        {
            var results = new List<Dictionary<string, object>>();

            try
            {
                using (SqlConnection connection = CreateSqlConnection())
                {
                    connection.Open();
                    Console.WriteLine("Connection successful");

                    SqlCommand command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT rzdesc AS CITY,
                               rzfeld1 AS 'SEAT_CAPACITY'
                        FROM IF_IF_raumzone
                        WHERE rzrzwonl = 'B' 
                          AND rznr NOT IN (281, 309, 310) 
                          AND rzfeld1 <> ''
                        ORDER BY rzdesc DESC";

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                var record = new Dictionary<string, object>
                        {
                            { "City", reader["CITY"] },
                            { "location", null }, // Added field with default value
                            { "Average Jan-Dec'22", null }, // Added field with default value
                            { "Average Jan-Dec'23", null }, // Added field with default value
                            { "1st Week", null }, // Added field with default value
                            { "2nd Week", null }, // Added field with default value
                            { "3rd Week", null }, // Added field with default value
                            { "22-Jan", null }, // Added field with default value
                            { "23-Jan", null }, // Added field with default value
                            { "24-Jan", null }, // Added field with default value
                            { "Occupancy % on 24-Jan", null }, // Added field with default value
                            { "Seat Capacity", reader["SEAT_CAPACITY"] }
                        };
                                results.Add(record);
                            }
                        }
                        else
                        {
                            Console.WriteLine("No data found.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, new { statuscode = 500, message = "An error occurred while processing your request." });
            }

            if (results.Count > 0)
            {
                return Ok(new { statuscode = 200, data = results });
            }
            else
            {
                return NotFound(new { statuscode = 404, message = "No data found." });
            }
        }

        private SqlConnection CreateSqlConnection()
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            return new SqlConnection(connectionString);
        }
    }
}
