/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using Microsoft.AspNetCore.Http;
using Rhetos.Logging;
using System;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rhetos.LightDMS
{
    public class Respond
    {
        private readonly ILogProvider _logProvider;
        
        public Respond(ILogProvider logProvider)
        {
            _logProvider = logProvider;
        }

        /// <summary>
        /// Logs the error details, and returns a generic error response with HTTP status code 500.
        /// </summary>
        public async Task InternalError(HttpContext context, Exception exception, [CallerFilePath] string sourceFilePath = null)
        {
            string userMessage = $"Internal server error occurred. See server log for more information. ({exception.GetType().Name}, {DateTime.Now:s})";
            await LogAndReturnError(context, exception.ToString(), userMessage, sourceFilePath, HttpStatusCode.InternalServerError, EventType.Error);
        }

        /// <summary>
        /// Logs the error details (if trace log enabled), and returns the provided error message with HTTP status code 400.
        /// </summary>
        public async Task BadRequest(HttpContext context, string error, [CallerFilePath] string sourceFilePath = null)
        {
            await LogAndReturnError(context, error, error, sourceFilePath, HttpStatusCode.BadRequest, EventType.Info);
        }

        private async Task LogAndReturnError(HttpContext context, string logMessage, string userMessage, string sourceFilePath, HttpStatusCode statusCode, EventType logEventType)
        {
            string loggerName = !string.IsNullOrEmpty(sourceFilePath)
                ? nameof(LightDMS) + "." + Path.GetFileNameWithoutExtension(sourceFilePath)
                : nameof(LightDMS);

            var logger = _logProvider.GetLogger(loggerName);
            logger.Write(logEventType, logMessage);

            //TODO: This locking issue might been solved on .NET 5. Removed this code after testing shows there is no issue here.
            //if (!logMessage.Contains(DownloadHelper.ResponseBlockedMessage))
            {
                context.Response.Clear();
                try
                {
                    context.Response.ContentType = "application/json;";
                    context.Response.StatusCode = (int)statusCode;
                }
                catch (Exception ex)
                {
                    logger.Error(ex.ToString());
                }
                await JsonSerializer.SerializeAsync(context.Response.Body, new { error = userMessage });
            }
        }

        public async Task Ok<T>(HttpContext context, T response)
        {
            context.Response.ContentType = "application/json;";
            context.Response.StatusCode = 200;
            await JsonSerializer.SerializeAsync(context.Response.Body, response);
        }
    }
}
