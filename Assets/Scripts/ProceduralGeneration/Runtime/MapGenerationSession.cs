namespace LegendsOfWarAndMagic.ProceduralGeneration.Runtime
{
    public static class MapGenerationSession
    {
        public static bool HasRequest { get; private set; }
        public static MapGenerationRequest CurrentRequest { get; private set; }

        public static void SetRequest(MapGenerationRequest request)
        {
            CurrentRequest = request ?? MapGenerationRequest.CreateDefault();
            HasRequest = true;
        }

        public static MapGenerationRequest GetRequestOrDefault()
        {
            return HasRequest && CurrentRequest != null
                ? CurrentRequest
                : MapGenerationRequest.CreateDefault();
        }

        public static void Clear()
        {
            CurrentRequest = null;
            HasRequest = false;
        }
    }
}
