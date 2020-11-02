using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UWPIoTAIApp
{
    public class CommonConstants
    {
        public static readonly string VoiceCommand_JobStart = "開始";
        public static readonly string VoiceCommand_JobStop = "完了";
        public static readonly string VoiceCommand_TakeAndUploadPicture = "撮影";
        public static readonly string VoiceCommand_CheckTarget = "確認";
        public static readonly string VoiceCommand_PreviewCamera = "カメラ";

        public static string IOTHUB_MSG_PROPERTY_KEY_APP = "app";
        public static string IOTHUB_MSG_PROPERTY_KEY_SENSOR_TYPE = "sensor_type";
        public static string IOTHUB_MSG_PROPERTY_KEY_MESSAGE_TYPE = "message_type";
        public static string IOTHUB_MSG_PROPERTY_APP_VALUE = "dynaedge";
        public static string IOTHUB_MSG_PROPERTY_MESSAGE_TYPE_LOCATION = "location";
        public static string IOTHUB_MSG_PROPERTY_MESSAGE_TYPE_SENSOR = "sensor";
        public static string IOTHUB_MSG_PROPERTY_MESSAGE_TYPE_TARGET = "target";
        public static string IOTHUB_MSG_PROPERTY_MESSAGE_TYPE_JOB = "job";


    }
}
