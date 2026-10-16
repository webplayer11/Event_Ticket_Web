import { useNavigate } from "react-router-dom";
import ExistingRegisterPage from "../RegisterPage";

export const RegisterPage = () => {
  const navigate = useNavigate();

  return (
    <ExistingRegisterPage
      onNavigateHome={() => navigate("/")}
      onNavigateLogin={() => navigate("/login")}
    />
  );
};
