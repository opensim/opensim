using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework
{
    public class Logger
    {
        public static Logger Instance = new Logger( false );
     
        public delegate void LoggerMethodDelegate();
        private delegate bool LoggerDelegate( LoggerMethodDelegate whatToDo );

        
        private LoggerDelegate m_delegate;
        
        public Logger( bool log )
        {
            if( log )
            {
                m_delegate = CatchAndLog;
            }
            else
            {
                m_delegate = DontCatch;
            }
        }
        
        public bool Wrap( LoggerMethodDelegate whatToDo )
        {
            return m_delegate( whatToDo );
        }


        private bool CatchAndLog(LoggerMethodDelegate whatToDo)
        {
            try
            {
                whatToDo();
                return true;
            }
            catch(Exception e)
            {
                System.Console.WriteLine( "Exception logged!!! Woah!!!!" );
                return false;
            }
        }

        private bool DontCatch(LoggerMethodDelegate whatToDo)
        {
            whatToDo();
            return true;
        }

        public class LoggerExample
        {
            public void TryWrap()
            {
                // This will log and ignore
                Logger log = new Logger(true);

                log.Wrap(delegate()
                             {
                                 Int16.Parse("waa!");
                             });

                // This will throw;            
                try
                {

                    log = new Logger(false);

                    log.Wrap(delegate()
                                 {
                                     Int16.Parse("waa!");
                                 });
                }
                catch
                {
                    System.Console.WriteLine("Example barfed!");
                }
            }
        }
    }              
}
