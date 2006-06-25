namespace Mono.Unix
{
        public class Catalog
        {
                public static void Init(string key, string value) {} 
                public static string GetString(string key) { return key; }
                public static string GetPluralString(string sing, string plu, int value)
                {
                   if (value == 1) return sing;
                   return plu;
                }
        }
        
        public class Syscall
        {
                public delegate void sighandler_t(int signal);

                public static void signal(int signal, sighandler_t handler) { }
                public static void symlink(string from, string to) { System.IO.File.Copy(from, to); }
                public static void unlink(string name) { System.IO.File.Delete(name); }
        }
}

namespace Mono.Unix.Native
{
        public class Stdlib
        {
                public delegate void SignalHandler(int signal);

                public static void signal(Signum signal, SignalHandler handler) { }
                //public static void symlink(string from, string to) { System.IO.File.Copy(from, to); }
                //public static void unlink(string name) { System.IO.File.Delete(name); }
        }

        public enum Signum
        {
                SIGQUIT,
                SIGTERM,
                SIGINT
        }
}
