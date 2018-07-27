using System;
using System.IO;
using System.Reflection;
using Akka.Configuration;
using Akka.Configuration.Hocon;

namespace Hocon.Test
{
    internal class Program
    {
        #region Non-public methods

        private static void Main( string[] args )
        {
            var assembly = typeof(Program).Assembly;

            var configContent = ReadConfigContent( assembly );

            var config = ConfigurationFactory.ParseString( configContent );

            Console.WriteLine( config );
        }

        private static string ReadConfigContent( Assembly assembly )
        {
            using ( var stream = assembly.GetManifestResourceStream( "Hocon.Test.Config.conf" ) )
            using ( var reader = new StreamReader( stream ) )
            {
                return reader.ReadToEnd();
            }
        }

        #endregion
    }
}
