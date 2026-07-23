#include <iostream>
#include <string>
#include <paddle_inference_api.h>

int main() {
    std::cout << "Paddle version: " << paddle::get_version() << std::endl;

    std::string base = "J:/Javer_Workplace/dev/LightOCR/models";
    std::string det_model = base + "/det/inference.json";
    std::string det_params = base + "/det/inference.pdiparams";
    std::string rec_model = base + "/rec/inference.json";
    std::string rec_params = base + "/rec/inference.pdiparams";

    // Try loading detection model with explicit file paths
    std::cout << "Loading detection model:" << std::endl;
    std::cout << "  prog: " << det_model << std::endl;
    std::cout << "  params: " << det_params << std::endl;

    paddle::AnalysisConfig config;
    config.SetModel(det_model, det_params);
    config.DisableGpu();
    config.EnableMKLDNN();
    config.SetCpuMathLibraryNumThreads(4);
    config.SwitchUseFeedFetchOps(false);
    config.SwitchSpecifyInputNames(true);

    try {
        auto predictor = paddle::CreatePaddlePredictor(config);
        std::cout << "  Detection model loaded successfully!" << std::endl;

        auto input_names = predictor->GetInputNames();
        std::cout << "  Input names: ";
        for (const auto& name : input_names) {
            std::cout << name << " ";
        }
        std::cout << std::endl;

        auto output_names = predictor->GetOutputNames();
        std::cout << "  Output names: ";
        for (const auto& name : output_names) {
            std::cout << name << " ";
        }
        std::cout << std::endl;
    } catch (const std::exception& e) {
        std::cerr << "  Failed: " << e.what() << std::endl;
        return 1;
    }

    // Try loading recognition model
    std::cout << "Loading recognition model:" << std::endl;
    std::cout << "  prog: " << rec_model << std::endl;
    std::cout << "  params: " << rec_params << std::endl;

    paddle::AnalysisConfig config2;
    config2.SetModel(rec_model, rec_params);
    config2.DisableGpu();
    config2.EnableMKLDNN();
    config2.SetCpuMathLibraryNumThreads(4);
    config2.SwitchUseFeedFetchOps(false);
    config2.SwitchSpecifyInputNames(true);

    try {
        auto predictor2 = paddle::CreatePaddlePredictor(config2);
        std::cout << "  Recognition model loaded successfully!" << std::endl;

        auto input_names = predictor2->GetInputNames();
        std::cout << "  Input names: ";
        for (const auto& name : input_names) {
            std::cout << name << " ";
        }
        std::cout << std::endl;
    } catch (const std::exception& e) {
        std::cerr << "  Failed: " << e.what() << std::endl;
        return 1;
    }

    std::cout << "Both models loaded successfully!" << std::endl;
    return 0;
}
