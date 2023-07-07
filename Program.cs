using Dapper;
using Hestia.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace tabs
{
    public class Program
    {
        public const string MIME_TEXT = "text/plain";
        public const string MIME_JSON = "application/json";
        public static readonly Encoding DEFAULT_ENCODING = Encoding.UTF8;
        public static readonly Func<IConfiguration, IDbConnection> CreateConnection = CreateSqliteConnection;
        public static readonly Func<IDbConnection, string, string, string, Task<int>> AddToDatabase = AddToSqlite;
        public static readonly Func<IDbConnection, string, uint, Task<int>> DelFromDatabase = DelFromSqlite;
        public static readonly Func<IDbConnection, string, uint, uint, long?, long?, string, Task<IEnumerable<object>>> GetFirstPageFromDatabase = GetFirstPageFromSqlite;
        public static readonly Func<IDbConnection, string, uint, uint, long?, long?, string, Task<IEnumerable<object>>> GetPageFromDatabase = GetPageFromSqlite;
        public static readonly Func<IDbConnection,Task> CreateTable = CreateSqliteTable;
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            using var db = CreateConnection(builder.Configuration);
            CreateTable(db);
            builder.Services.AddCors(builder => builder.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
            var app = builder.Build();
            app.UseFileServer();
            app.UseWhen((ctx) => { return ctx.Request.Path.StartsWithSegments("/api"); }, app => {
                app.UseCors();
                app.Use(Auth);
            });
            app.MapGet("/api/add", Add);
            app.MapGet("/api/del", Del);
            app.MapGet("/api/page", Page);
            app.Run();
        }

        public static async Task Unauthorized(HttpContext ctx)
        {
            await Response(ctx, HttpStatusCode.Unauthorized,$"{HttpStatusCode.Unauthorized}",MIME_TEXT);
        }

        public static async Task BadRequest(HttpContext ctx,string message)
        {
            await Response(ctx,HttpStatusCode.BadRequest,message, MIME_TEXT);
        }

        public static async Task Json(HttpContext ctx,string json)
        {
            await Response(ctx, HttpStatusCode.OK, json, MIME_JSON);
        }

        public static async Task Response(HttpContext ctx,HttpStatusCode code, string body, string type)
        {
            ctx.Response.StatusCode = (int)code;
            if (string.IsNullOrEmpty(body)) { return; }
            if(!string.IsNullOrEmpty(type)) 
            {
                ctx.Response.ContentType = type;
            }
            await ctx.Response.WriteAsync(body);
        }

        public static async Task Auth(HttpContext ctx, RequestDelegate next)
        {
            var user = ctx.Request.Headers["user"].At();
            if(string.IsNullOrEmpty(user)) { await Unauthorized(ctx); return; }
            var token = ctx.Request.Headers["token"].At();
            if (string.IsNullOrEmpty(token)) { await Unauthorized(ctx); return; }

            var configuration = ctx.RequestServices.GetRequiredService<IConfiguration>();
            var salt = configuration.GetValue<string>("salt", null);
            if(string.IsNullOrEmpty(salt)) { salt = "tabs"; }

            var mac = Hestia.Security.MAC.HMAC_SHA256(salt.Transform(DEFAULT_ENCODING.GetBytes), user.Transform(DEFAULT_ENCODING.GetBytes)).Transform(Convert.ToHexString);
            if (!string.Equals(mac, token, StringComparison.OrdinalIgnoreCase)){ await Unauthorized(ctx); return; }

            var identity = new ClaimsIdentity();
            identity.AddClaim(new Claim(ClaimTypes.Name, user));
            ctx.User = new ClaimsPrincipal(identity);

            await next.Invoke(ctx);
        }           

        public static IDbConnection CreateSqliteConnection(IConfiguration configuration)
        {
            var connection = configuration.GetValue<string>("sqlite3", null);
            if (string.IsNullOrEmpty(connection)) { connection = "db.sqlite3"; }
            return new SqliteConnection($"Data Source={connection};");
        }      

        public static async Task<int> AddToSqlite(IDbConnection db,string user,string title,string url)
        {
            var args = new DynamicParameters();
            args.Add("@timestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds());
            args.Add("@user", user);
            args.Add("@title", title);
            args.Add("@url", url);
            var sql = @"INSERT INTO ""tabs"" (""title"",""url"",""timestamp"",""user"") VALUES (@title,@url,@timestamp,@user)";
            return await db.ExecuteAsync(sql, args);
        }

        public static async Task<int> DelFromSqlite(IDbConnection db, string user, uint id)
        {
            var args = new DynamicParameters();
            args.Add("@user", user);
            args.Add("@id", id);
            var sql = @"DELETE FROM ""tabs"" WHERE ""user"" = @user AND ""id"" = @id";
            return await db.ExecuteAsync(sql, args);
        }

        public static string BuildGetPageSqlFromSqlite(long? from, long? to, string search,string postfix)
        {
            var sql = new StringBuilder(@"SELECT ""id"",""title"",""url"",""timestamp"" FROM ""tabs"" WHERE ""user"" = @user");
            if (from.HasValue && to.HasValue)
            {
                sql.Append(@" AND ""timestamp"" >= @from AND ""timestamp"" < @to");
            }
            if (!string.IsNullOrEmpty(search))
            {
                sql.Append(@" AND (""title"" LIKE @search OR ""url"" LIKE @search)");
            }
            if (!string.IsNullOrEmpty(postfix))
            {
                sql.Append(postfix);
            }
            return sql.ToString();
        }

        public static DynamicParameters BuildGetPageArgsFromSqlite(string user, uint cursor, uint size, long? from, long? to, string search)
        {
            var args = new DynamicParameters();
            args.Add("@user", user);
            args.Add("@cursor", cursor);
            args.Add("@size", size);
            if (from.HasValue && to.HasValue)
            {
                args.Add("@from", from.Value);
                args.Add("@to", to.Value);
            }
            if (!string.IsNullOrEmpty(search))
            {
                args.Add("@search", $"%{search}%");
            }
            return args;
        }

        public static async Task<IEnumerable<object>> GetFirstPageFromSqlite(IDbConnection db,string user,uint cursor,uint size,long? from,long? to,string search)
        {   
            var sql = BuildGetPageSqlFromSqlite(from, to, search, @" ORDER BY ""id"" DESC LIMIT 2*@size");
            var args = BuildGetPageArgsFromSqlite(user, cursor, size, from, to, search);

            return await db.QueryAsync<object>(sql.ToString(), args);
        }

        public static async Task<IEnumerable<object>> GetPageFromSqlite(IDbConnection db, string user, uint cursor, uint size, long? from, long? to, string search)
        {
            var sql = new StringBuilder(@"WITH ""previous"" AS ( ");

            sql.Append(BuildGetPageSqlFromSqlite(from, to, search, @" AND ""id"" > @cursor ORDER BY ""id"" ASC LIMIT @size"));
            sql.Append(@" ), ""next"" AS ( ");
            sql.Append(BuildGetPageSqlFromSqlite(from, to, search, @" AND ""id"" <= @cursor ORDER BY ""id"" DESC LIMIT 2*@size"));
            sql.Append(@") SELECT ""id"",""title"",""url"",""timestamp"" FROM ""previous"" UNION SELECT ""id"",""title"",""url"",""timestamp"" FROM ""next""");

            var args = BuildGetPageArgsFromSqlite(user, cursor, size, from, to, search);
            return await db.QueryAsync<object>(sql.ToString(), args);
        }

        public static async Task CreateSqliteTable(IDbConnection db)
        {
            var sql = @"CREATE TABLE IF NOT EXISTS ""tabs"" (
  ""id"" integer NOT NULL COLLATE BINARY PRIMARY KEY AUTOINCREMENT,
  ""title"" text NOT NULL DEFAULT '' COLLATE BINARY,
  ""url"" text NOT NULL DEFAULT '' COLLATE BINARY,
  ""timestamp"" integer NOT NULL COLLATE BINARY,
  ""user"" text NOT NULL DEFAULT '' COLLATE BINARY
)";
            await db.ExecuteAsync(sql);
        }

        public static async Task Add(HttpContext ctx)
        {
            var url = ctx.Request.Query["url"].At();
            if (string.IsNullOrEmpty(url)) { await BadRequest(ctx,"url");return; }
            var title = ctx.Request.Query["title"].At();
            if(string.IsNullOrEmpty(title)) { title = url; }
            var user = ctx.User.Identity.Name;

            var configuration = ctx.RequestServices.GetRequiredService<IConfiguration>();
            using var db = CreateConnection(configuration);
           
            var rows = await AddToDatabase(db,user,title.Transform(WebUtility.UrlDecode),url.Transform(WebUtility.UrlDecode));

            ctx.Response.StatusCode = (int)(rows == 0 ? HttpStatusCode.OK : HttpStatusCode.Created);
        }

        public static async Task Del(HttpContext ctx)
        {
            var id = ctx.Request.Query["id"].At()?.ToUnsignedInt();
            if (!id.HasValue) { await BadRequest(ctx, "id"); return; }            
            var user = ctx.User.Identity.Name;

            var configuration = ctx.RequestServices.GetRequiredService<IConfiguration>();
            using var db = CreateConnection(configuration);

            var rows = await DelFromDatabase(db, user, id.Value);

            ctx.Response.StatusCode = (int)(rows == 0 ? HttpStatusCode.OK : HttpStatusCode.NoContent);
        }

        public static async Task Page(HttpContext ctx)
        {
            var cursor = ctx.Request.Query["cursor"].At()?.ToUnsignedInt() ?? 0u;
            var size = ctx.Request.Query["size"].At()?.ToUnsignedInt() ?? 10u;
            var from = ctx.Request.Query["from"].At()?.ToLong();
            var to = ctx.Request.Query["to"].At()?.ToLong();
            var search = ctx.Request.Query["search"].At();
            var user = ctx.User.Identity.Name;

            var configuration = ctx.RequestServices.GetRequiredService<IConfiguration>();
            using var db = CreateConnection(configuration);

            var list = new List<object>();

            var func = (cursor == 0) ? GetFirstPageFromDatabase : GetPageFromDatabase;

            list.AddRange(await func(db,user,cursor,size,from,to,search));

            var json = Utility.ToJson(list);

            await Json(ctx, json);
        }
    }
}

