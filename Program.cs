using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using ConsolaEnvioSMS.Models;
using ConsolaEnvioSMS.DAL;

namespace ConsolaEnvioSMS
{
    public class Program
    {
        static HttpClient client = new HttpClient();

        static void Main(string[] args)
        {

            bool createdNew;
            var waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, "CF2D4313-33DE-489D-9721-6AFF69841DEA", out createdNew);
            var signaled = false;

            if (!createdNew)
            {
                Log("Inform other process to stop.");
                waitHandle.Set();
                Log("Informer exited.");

                return;
            }

            do
            {
                ProcesoVerificarEnvioSMS();
                _ = waitHandle.WaitOne(TimeSpan.FromSeconds(120));
                ProcesoVerificarRespuestaSMS();
                signaled = waitHandle.WaitOne(TimeSpan.FromSeconds(90));


            } while (!signaled);

            // The above loop with an interceptor could also be replaced by an endless waiter
            waitHandle.WaitOne();

            Log("Got signal to kill myself.");

            Console.WriteLine("Kill!");

        }

        private static void Log(string message)
        {
            Console.WriteLine(DateTime.Now + ": " + message);
        }

        static void ProcesoVerificarEnvioSMS()
        {
            Log("Verifico Envio SMS");

            var MensajeDAL = new MensajeDAL();
            var lstEnvio = MensajeDAL.ObtenerMensajesporEnviar();

            if (lstEnvio.Count() > 0)
                Console.WriteLine("Mensajes # " + lstEnvio.Count() + " a Enviar");
            {
                lstEnvio.ForEach(item =>
                {
                    //Console.WriteLine("Envio al " +item.senderId );
                    EjecutaApi_EnvioSMS(item.senderId, item.celular, item.mensaje);
                });
            }
        }

        static void EjecutaApi_EnvioSMS(Int32 senderId, string celular, string mensaje) //Adding Event  
        {
            try
            {
                using (var client = new WebClient())
                {
                    //parametros
                    Mensaje contenido = new Mensaje
                    {
                        usuario = "KyodaiApiTRS",
                        password = "K10dsog4vsa",
                        celular = celular,
                        mensaje = mensaje,
                        senderId = senderId
                    };

                    client.Headers.Add("Content-Type:application/json");
                    client.Headers.Add("Accept:application/json");

                    string URI = "https://ws.intico.com.pe:8181/rest/webresources/envioSMS/smsv2";
                    var parametros = JsonConvert.SerializeObject(contenido, Formatting.Indented);
                    var result = client.UploadString(URI, "POST", parametros);
                    var sjson = JsonConvert.DeserializeObject<RootObject>(result);
                    var MesDAL = new MensajeDAL();

                    if (sjson.estado == 1)
                    {
                        MesDAL.GrabaCodigoRespuesta(sjson.estado, senderId, sjson.codigo);
                    }
                    else
                    {
                        //Grabar si existe error 
                        MesDAL.GrabaCodigoRespuesta(sjson.estado, senderId, sjson.error);
                    }

                    Console.WriteLine(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

        }

        static void ProcesoVerificarRespuestaSMS()
        {
            Log("Verifico Respuesta SMS");
            var MensajeDAL = new MensajeDAL();
            var lstRespuesta = MensajeDAL.ObteneRespuestasdeMensajes();

            if (lstRespuesta.Count() > 0)
                Console.WriteLine("Respuestas # " + lstRespuesta.Count() + " a Tomar");
            {
                lstRespuesta.ForEach(item =>
                {
                    EjecutaApi_RespuestaSMS(item.codigo, item.senderId);
                });
            }

        }

        static void EjecutaApi_RespuestaSMS(string codigo, Int32 senderId) //Adding Event  
        {
            try
            {
                using (var client = new WebClient())
                {
                    //parametros
                    Mensaje contenido = new Mensaje
                    {
                        usuario = "KyodaiApiTRS",
                        password = "K10dsog4vsa",
                        codigo = codigo
                    };

                    client.Headers.Add("Content-Type:application/json");
                    client.Headers.Add("Accept:application/json");

                    string URI = "https://ws.intico.com.pe:8181/rest/webresources/envioSMS/confirmav2";
                    var parametros = JsonConvert.SerializeObject(contenido, Formatting.Indented);
                    var result = client.UploadString(URI, "POST", parametros);
                    var sjson = JsonConvert.DeserializeObject<RootObject>(result);

                    if ((sjson.estado == 1) && (sjson.flag_entrega.Equals("C") || sjson.flag_entrega.Equals("R")))
                    {
                        var MensajeDAL = new MensajeDAL();
                        MensajeDAL.GrabaConfirmacionRespuesta(sjson.flag_entrega, sjson.fecha_entrega, sjson.hora_entrega, senderId);
                    }

                    Console.WriteLine(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

        }

    }
    public class RootObject
    {
        public int estado { get; set; }
        public string codigo { get; set; }
        public string error { get; set; }
        public string flag_entrega { get; set; }
        public string fecha_entrega { get; set; }
        public string hora_entrega { get; set; }

    }
}
