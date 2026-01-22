using OpenSim.Tools.ControlPanel.Models;

namespace OpenSim.Tools.WebControlPanel.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }

        public static ApiResponse<T> SuccessResult(T data, string message = null)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Data = data,
                Message = message
            };
        }

        public static ApiResponse<T> ErrorResult(string error)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Error = error
            };
        }
    }

    public class CreateSimulationRequest
    {
        public string Name { get; set; } = string.Empty;
        public string RegionName { get; set; } = string.Empty;
        public int HttpPort { get; set; } = 9000;
        public int InternalPort { get; set; } = 9000;
        public string ExternalHostName { get; set; } = "127.0.0.1";
        public int MaxAvatars { get; set; } = 100;
        public int MaxObjects { get; set; } = 15000;
        public int LocationX { get; set; } = 1000;
        public int LocationY { get; set; } = 1000;
    }
}